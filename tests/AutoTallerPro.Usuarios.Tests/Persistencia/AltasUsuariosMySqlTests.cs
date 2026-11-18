using System.Collections.Concurrent;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

public sealed partial class PersistenciaMySqlTests
{
    [MySqlFact]
    public async Task Alta_GuardaFichaAuditoriaYCorreoYPuedeRepetirseConLaMismaClave()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor);
        var servicio = Servicio(keycloak, servicios);
        var clave = Guid.NewGuid();
        var datos = Solicitud("usuario-" + Guid.NewGuid().ToString("N"), Roles.Administrador);

        var creado = await servicio.CrearAsync(actor, clave, datos, default);
        Assert.Equal(creado, await servicio.CrearAsync(actor, clave, datos, default));
        Assert.Equal("correoSolicitado", creado.EstadoInvitacion);
        var (sujeto, cuenta) = Assert.Single(keycloak.Cuentas, p => p.Key != actor.Sujeto);
        Assert.NotNull(cuenta.CorrelacionAlta);
        Assert.False(cuenta.CorreoVerificado);
        Assert.Equal([Roles.Administrador], await keycloak.ObtenerRolesAsync(sujeto));
        Assert.Equal(1, keycloak.SolicitudesCorreo);
        Assert.Equal(1, keycloak.Altas);

        var ficha = await servicios.GetRequiredService<IFichasAdministrativas>().BuscarPorIdAsync(creado.Id);
        Assert.Equal(new IdentidadKeycloak(actor.Emisor, sujeto), ficha!.Identidad);
        Assert.Equal(actor, ficha.CreadoPor);
        var eventos = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        var evento = Assert.Single(eventos, e => e.Actor == actor);
        Assert.Equal("USUARIO_CREADO", evento.Accion);
        Assert.Equal(creado.Id.ToString(), evento.ObjetivoId);

        var distinta = await Assert.ThrowsAsync<OperacionUsuariosException>(() =>
            servicio.CrearAsync(actor, clave, datos with { Nombre = "Otro" }, default));
        Assert.Equal(ErrorUsuarios.ClaveReutilizada, distinta.Error);
        var duplicada = await Assert.ThrowsAsync<OperacionUsuariosException>(() =>
            servicio.CrearAsync(actor, Guid.NewGuid(), datos, default));
        Assert.Equal(ErrorUsuarios.CorreoDuplicado, duplicada.Error);
    }

    [MySqlFact]
    public async Task Alta_ConcurrenciaResuelveUsernameYProtegeLaMismaClave()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor);
        var servicio = Servicio(keycloak, alcance.ServiceProvider);
        var nombre = "concurrente-" + Guid.NewGuid().ToString("N");
        var primeraClave = Guid.NewGuid();
        var primeraEntrada = Solicitud(nombre);
        var segundaDisponible = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var comenzaron = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contador = 0;
        keycloak.AntesDeCrear = async cuenta =>
        {
            if (cuenta.Username == nombre)
            {
                if (Interlocked.Increment(ref contador) == 2)
                {
                    segundaDisponible.TrySetResult();
                }
                comenzaron.TrySetResult();
                await segundaDisponible.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        };

        var primero = servicio.CrearAsync(actor, primeraClave, primeraEntrada, default);
        await comenzaron.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var repetido = await Assert.ThrowsAsync<OperacionUsuariosException>(() =>
                servicio.CrearAsync(actor, primeraClave, primeraEntrada, default));
            Assert.Equal(ErrorUsuarios.AltaEnCurso, repetido.Error);
            var mismoCorreo = await Assert.ThrowsAsync<OperacionUsuariosException>(() =>
                servicio.CrearAsync(actor, Guid.NewGuid(), primeraEntrada, default));
            Assert.Equal(ErrorUsuarios.CorreoDuplicado, mismoCorreo.Error);

            var segundo = servicio.CrearAsync(actor, Guid.NewGuid(), primeraEntrada with { Email = nombre + "@otro.invalid" }, default);
            var altas = await Task.WhenAll(primero, segundo);
            Assert.Equal([nombre, nombre + "-1"], altas.Select(a => a.Username).Order().ToArray());
            Assert.All(altas, a => Assert.Equal("correoSolicitado", a.EstadoInvitacion));
            Assert.Equal(2, keycloak.Altas);
        }
        finally
        {
            segundaDisponible.TrySetResult();
            await primero;
        }
    }

    [MySqlFact]
    public async Task Alta_TimeoutRecuperaSoloLaCuentaCreadaPorEsaOperacion()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor) { TimeoutAlCrear = true };
        var servicio = Servicio(keycloak, alcance.ServiceProvider);
        var clave = Guid.NewGuid();
        var datos = Solicitud("recuperable-" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<OperacionIdentidadException>(() => servicio.CrearAsync(actor, clave, datos, default));
        var creado = await servicio.CrearAsync(actor, clave, datos, default);

        Assert.Equal(1, keycloak.Altas);
        Assert.Equal(1, keycloak.SolicitudesCorreo);
        Assert.Equal("correoSolicitado", creado.EstadoInvitacion);
    }

    [MySqlFact]
    public async Task Alta_ConflictoConCuentaAjenaTrasTimeoutNoVinculaNiAsignaRol()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor);
        var servicio = Servicio(keycloak, alcance.ServiceProvider);
        var clave = Guid.NewGuid();
        var datos = Solicitud("ajeno-" + Guid.NewGuid().ToString("N"));
        keycloak.AntesDeCrear = _ => throw new OperacionIdentidadException("Crear", ErrorIdentidad.TiempoAgotado,
            resultadoIncierto: true);
        await Assert.ThrowsAsync<OperacionIdentidadException>(() => servicio.CrearAsync(actor, clave, datos, default));
        string ajeno = Guid.NewGuid().ToString();
        keycloak.Cuentas[ajeno] = new(ajeno, datos.Email!.Split('@')[0], datos.Email, "Otra", "Cuenta", true, false, []);

        var error = await Assert.ThrowsAsync<OperacionUsuariosException>(() =>
            servicio.CrearAsync(actor, clave, datos, default));
        Assert.Equal(ErrorUsuarios.CorreoDuplicado, error.Error);
        Assert.Empty(await keycloak.ObtenerRolesAsync(ajeno));
        Assert.Empty(await alcance.ServiceProvider.GetRequiredService<IConsultaFichasUsuarios>().ListarAsync(actor.Emisor, default));
    }

    [MySqlFact]
    public async Task Alta_FalloDeAuditoriaRevierteFichaYReintentoReutilizaKeycloak()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor);
        var servicios = alcance.ServiceProvider;
        var servicio = Servicio(keycloak, servicios);
        var clave = Guid.NewGuid();
        var datos = Solicitud("rollback-" + Guid.NewGuid().ToString("N"));

        await using var conexion = mysql.CrearConexion();
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"""
            ALTER TABLE AuditoriaAdministrativa ADD CONSTRAINT CK_P44_prueba CHECK (ActorSujeto <> '{actor.Sujeto}');
            """;
        await comando.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<MySqlException>(() => servicio.CrearAsync(actor, clave, datos, default));
            Assert.Empty(await servicios.GetRequiredService<IConsultaFichasUsuarios>().ListarAsync(actor.Emisor, default));
            Assert.Equal(0, keycloak.SolicitudesCorreo);
        }
        finally
        {
            comando.CommandText = "ALTER TABLE AuditoriaAdministrativa DROP CHECK CK_P44_prueba;";
            await comando.ExecuteNonQueryAsync();
        }

        var recuperado = await servicio.CrearAsync(actor, clave, datos, default);
        Assert.Equal(1, keycloak.Altas);
        Assert.Equal(1, keycloak.SolicitudesCorreo);
        Assert.Equal(recuperado.Id, Assert.Single(await servicios.GetRequiredService<IConsultaFichasUsuarios>()
            .ListarAsync(actor.Emisor, default)).Id);
    }

    [MySqlFact]
    public async Task Alta_FalloDefinitivoDeCorreoQuedaPendienteYElMismoIntentoRecuperaElEnvio()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor) { ErrorCorreo = new("Invitar", ErrorIdentidad.Prohibido, 403) };
        var servicio = Servicio(keycloak, alcance.ServiceProvider);
        var clave = Guid.NewGuid();
        var datos = Solicitud("pendiente-" + Guid.NewGuid().ToString("N"));

        var pendiente = await servicio.CrearAsync(actor, clave, datos, default);
        keycloak.ErrorCorreo = null;
        var recuperado = await servicio.CrearAsync(actor, clave, datos, default);

        Assert.Equal("pendiente", pendiente.EstadoInvitacion);
        Assert.Equal("correoSolicitado", recuperado.EstadoInvitacion);
        Assert.Equal(pendiente.Id, recuperado.Id);
        Assert.Equal(1, keycloak.Altas);
        Assert.Equal(2, keycloak.SolicitudesCorreo);
    }

    [MySqlFact]
    public async Task Alta_CorreoInciertoConservaCuentaYEvitaraUnSegundoCorreoAutomatico()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var keycloak = new KeycloakPrueba(actor)
        {
            ErrorCorreo = new("Invitar", ErrorIdentidad.TiempoAgotado, resultadoIncierto: true)
        };
        var servicio = Servicio(keycloak, alcance.ServiceProvider);
        var clave = Guid.NewGuid();
        var datos = Solicitud("incierto-" + Guid.NewGuid().ToString("N"));

        var incierto = await servicio.CrearAsync(actor, clave, datos, default);
        Assert.Equal("envioIncierto", incierto.EstadoInvitacion);
        Assert.Equal(incierto, await servicio.CrearAsync(actor, clave, datos, default));
        Assert.Equal(1, keycloak.Altas);
        Assert.Equal(1, keycloak.SolicitudesCorreo);
    }

    [MySqlFact]
    public async Task Consultas_FiltranAntesDePaginarYSeparanActividadDeInvitacion()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var servicios = alcance.ServiceProvider;
        var keycloak = new KeycloakPrueba(actor);
        var altas = Servicio(keycloak, servicios);
        var consulta = new ConsultaUsuariosServicio(servicios.GetRequiredService<IConsultaFichasUsuarios>(), keycloak);
        var primero = await altas.CrearAsync(actor, Guid.NewGuid(), Solicitud("ana-primera", Roles.Administrador), default);
        var segundo = await altas.CrearAsync(actor, Guid.NewGuid(), Solicitud("ana-segunda"), default);
        var tercero = await altas.CrearAsync(actor, Guid.NewGuid(), Solicitud("usuario-ultimo"), default);
        var id = (await keycloak.BuscarPorUsernameAsync(segundo.Username))!.Id;
        await keycloak.CambiarHabilitacionAsync(id, false);
        keycloak.Cuentas[id] = keycloak.Cuentas[id] with { CorreoVerificado = true, AccionesRequeridas = [] };

        var pagina = await consulta.ListarAsync(actor.Emisor, "ANA", null, null, 2, 1, default);
        Assert.Equal(2, pagina.Total);
        Assert.Equal(segundo.Id, Assert.Single(pagina.Usuarios).Id);
        var recepcionistas = await consulta.ListarAsync(actor.Emisor, "@ejemplo.invalid", Roles.Recepcionista, true, 1, 1, default);
        Assert.Equal(1, recepcionistas.Total);
        Assert.Equal(tercero.Id, Assert.Single(recepcionistas.Usuarios).Id);
        Assert.Equal("correoSolicitado", (await consulta.DetalleAsync(actor.Emisor, primero.Id, default))!.EstadoInvitacion);
        var inactivo = (await consulta.DetalleAsync(actor.Emisor, segundo.Id, default))!;
        Assert.False(inactivo.Activo);
        Assert.Equal("completada", inactivo.EstadoInvitacion);
        Assert.Null(await consulta.DetalleAsync(actor.Emisor, int.MaxValue, default));
        Assert.Equal(new ResumenUsuarios(3, 2, 1), await consulta.ResumenAsync(actor.Emisor, default));
    }

    private static IdentidadKeycloak ActorAlta() => new(Emisor + "-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString());
    private static SolicitudAltaUsuario Solicitud(string usuario, string rol = Roles.Recepcionista) =>
        new(usuario + "@ejemplo.invalid", rol, "Nombre", "Apellido");

    private static AltaUsuariosServicio Servicio(KeycloakPrueba keycloak, IServiceProvider proveedor) =>
        new(keycloak, proveedor.GetRequiredService<IAltasUsuarios>(), TimeProvider.System);
}

internal sealed class KeycloakPrueba
    : IIdentidadesKeycloak
{
    private readonly ConcurrentDictionary<string, string[]> _roles = new();
    private int _solicitudesCorreo;
    private int _altas;

    internal KeycloakPrueba(IdentidadKeycloak actor)
    {
        Cuentas[actor.Sujeto] = new(actor.Sujeto, "administrador", null, null, null, true, true, []);
        _roles[actor.Sujeto] = [Roles.Administrador];
    }

    internal ConcurrentDictionary<string, CuentaKeycloak> Cuentas { get; } = new();
    internal int SolicitudesCorreo => _solicitudesCorreo;
    internal int Altas => _altas;
    internal bool TimeoutAlCrear { get; init; }
    internal Func<NuevaCuentaKeycloak, Task>? AntesDeCrear { get; set; }
    internal OperacionIdentidadException? ErrorCorreo { get; set; }

    public Task<CuentaKeycloak?> ObtenerAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cuentas.GetValueOrDefault(id));

    public Task<CuentaKeycloak?> BuscarPorUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cuentas.Values.SingleOrDefault(c => c.Username == username));

    public Task<CuentaKeycloak?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cuentas.Values.SingleOrDefault(c => c.Correo == correo));

    public async Task<bool> ExisteUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        await BuscarPorUsernameAsync(username, cancellationToken) is not null;

    public async Task<string> CrearAsync(NuevaCuentaKeycloak cuenta, CancellationToken cancellationToken = default)
    {
        if (AntesDeCrear is { } antes)
        {
            await antes(cuenta);
        }

        string id = Guid.NewGuid().ToString();
        lock (Cuentas)
        {
            if (Cuentas.Values.Any(c => c.Correo == cuenta.Correo))
            {
                throw new OperacionIdentidadException("Crear", ErrorIdentidad.Conflicto, 409, campoEnConflicto: CampoIdentidad.Correo);
            }

            if (Cuentas.Values.Any(c => c.Username == cuenta.Username))
            {
                throw new OperacionIdentidadException("Crear", ErrorIdentidad.Conflicto, 409, campoEnConflicto: CampoIdentidad.Username);
            }

            Cuentas[id] = new(id, cuenta.Username, cuenta.Correo, cuenta.Nombre, cuenta.Apellido, true, false,
                ["VERIFY_EMAIL", "UPDATE_PASSWORD"], cuenta.CorrelacionAlta);
            Interlocked.Increment(ref _altas);
        }

        return TimeoutAlCrear
            ? throw new OperacionIdentidadException("Crear", ErrorIdentidad.TiempoAgotado, resultadoIncierto: true)
            : id;
    }

    public Task ActualizarDatosAsync(string id, DatosCuentaKeycloak datos, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<string>> ObtenerRolesAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(_roles.GetValueOrDefault(id, []));

    public Task AsignarRolAsync(string id, string rol, CancellationToken cancellationToken = default)
    {
        _roles[id] = [rol];
        return Task.CompletedTask;
    }

    public Task RetirarRolAsync(string id, string rol, CancellationToken cancellationToken = default)
    {
        _roles[id] = [];
        return Task.CompletedTask;
    }

    public Task CambiarHabilitacionAsync(string id, bool habilitada, CancellationToken cancellationToken = default)
    {
        Cuentas[id] = Cuentas[id] with { Habilitada = habilitada };
        return Task.CompletedTask;
    }

    public Task CerrarSesionesAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SolicitarInvitacionAsync(string id, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _solicitudesCorreo);
        return ErrorCorreo is { } error ? Task.FromException(error) : Task.CompletedTask;
    }
}

