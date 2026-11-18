using AutoTallerPro.Usuarios.Application.Fachadas;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

public sealed partial class PersistenciaMySqlTests
{
    [MySqlFact]
    public async Task Administrar_ActualizaCorreoSoloRolesDeNegocioYRegistraElCambioUnaVez()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        var sujeto = await CuentaGestionadaAsync(kc, Roles.Administrador, true);
        kc.RolesDirectos[sujeto] = ["rol-tecnico"];
        kc.Sesiones[sujeto] = true;
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        var servicio = Administracion(kc, servicios);
        string correo = "nueva-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";
        var datos = new EdicionUsuario(correo, Roles.Recepcionista, true);
        var clave = Guid.NewGuid();
        string username = kc.Cuentas[sujeto].Username;

        await ExigirExitoAsync(servicio.EditarAsync(actor, id, clave, datos, default));
        await ExigirExitoAsync(servicio.EditarAsync(actor, id, clave, datos, default));

        var cuenta = kc.Cuentas[sujeto];
        Assert.Equal(correo, cuenta.Correo);
        Assert.Equal(username, cuenta.Username);
        Assert.False(cuenta.CorreoVerificado);
        Assert.Equal(["VERIFY_EMAIL"], cuenta.AccionesRequeridas);
        Assert.Equal([Roles.Recepcionista, "rol-tecnico"], await kc.ObtenerRolesAsync(sujeto));
        Assert.False(await kc.TieneSesionesAsync(sujeto));
        Assert.Equal(1, kc.SesionesCerradas);
        var registros = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        var exito = Assert.Single(registros, e => e.Actor == actor && e.Resultado == ResultadoAuditoria.Exitoso);
        Assert.Equal("USUARIO_EDITADO", exito.Accion);
        Assert.NotNull(exito.CorrelacionId);
        var ficha = await servicios.GetRequiredService<IFichasAdministrativas>().BuscarPorIdAsync(id);
        Assert.Equal(actor, ficha!.ModificadoPor);
    }

    [MySqlFact]
    public async Task Administrar_ProtegeCuentaPropiaYNoCuentaAdministradoresPendientes()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        var pendiente = await CuentaGestionadaAsync(kc, Roles.Administrador, false);
        kc.Contrasenas[pendiente] = true;
        await FichaGestionadaAsync(servicios, actor, pendiente);
        int propio = await FichaGestionadaAsync(servicios, actor, actor.Sujeto);
        var servicio = Administracion(kc, servicios);

        await VerificarErrorAsync(ErrorUsuarios.CuentaPropia, () =>
            servicio.DesactivarAsync(actor, propio, Guid.NewGuid(), default));
        string correo = "otro-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";
        await VerificarErrorAsync(ErrorUsuarios.CuentaPropia, () => servicio.EditarAsync(actor, propio,
            Guid.NewGuid(), new EdicionUsuario(correo, Roles.Recepcionista, true), default));
        await VerificarErrorAsync(ErrorUsuarios.UltimoAdministrador, () => servicio.EditarAsync(actor, propio,
            Guid.NewGuid(), new EdicionUsuario(correo, Roles.Administrador, true), default));

        Assert.True(kc.Cuentas[actor.Sujeto].Habilitada);
        var eventos = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        Assert.Contains(eventos, e => e.Actor == actor && e.Resultado == ResultadoAuditoria.Fallido
            && e.CodigoMotivo == "ULTIMO_ADMINISTRADOR");
    }

    [MySqlFact]
    public async Task Administrar_EntreDosInstanciasElActorEnEsperaDebeSeguirSiendoAdministrador()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        using var otroAlcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujetoOtro = await CuentaGestionadaAsync(kc, Roles.Administrador, true);
        var otro = new IdentidadKeycloak(actor.Emisor, sujetoOtro);
        int primero = await FichaGestionadaAsync(servicios, actor, actor.Sujeto);
        int segundo = await FichaGestionadaAsync(servicios, actor, sujetoOtro);
        var servicio = Administracion(kc, servicios);
        var otraInstancia = Administracion(kc, otroAlcance.ServiceProvider);
        var empezo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kc.DespuesDeCambiarHabilitacion = async (_, habilitada) =>
        {
            if (!habilitada)
            {
                empezo.TrySetResult();
                await terminar.Task.WaitAsync(TimeSpan.FromSeconds(8));
            }
        };

        var primera = servicio.DesactivarAsync(actor, segundo, Guid.NewGuid(), default);
        await empezo.Task.WaitAsync(TimeSpan.FromSeconds(8));
        try
        {
            var inversa = otraInstancia.DesactivarAsync(otro, primero, Guid.NewGuid(), default);
            terminar.TrySetResult();
            await ExigirExitoAsync(primera);
            await VerificarErrorAsync(ErrorUsuarios.AdministradorNoVigente, () => inversa);
        }
        finally
        {
            terminar.TrySetResult();
        }

        Assert.True(kc.Cuentas[actor.Sujeto].Habilitada);
        Assert.False(kc.Cuentas[sujetoOtro].Habilitada);
    }

    [MySqlFact]
    public async Task Administrar_DesactivaRevisaEstadoYReactivaConReglasCompartidas()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, true);
        kc.Sesiones[sujeto] = true;
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        var servicio = Administracion(kc, servicios);

        await VerificarErrorAsync(ErrorUsuarios.EstadoSinCambio, () =>
            servicio.ReactivarAsync(actor, id, Guid.NewGuid(), default));
        await ExigirExitoAsync(servicio.EditarAsync(actor, id, Guid.NewGuid(),
            new(kc.Cuentas[sujeto].Correo, Roles.Recepcionista, false), default));
        Assert.False(kc.Cuentas[sujeto].Habilitada);
        Assert.False(await kc.TieneSesionesAsync(sujeto));
        await VerificarErrorAsync(ErrorUsuarios.EstadoSinCambio, () =>
            servicio.DesactivarAsync(actor, id, Guid.NewGuid(), default));
        await ExigirExitoAsync(servicio.ReactivarAsync(actor, id, Guid.NewGuid(), default));
        Assert.True(kc.Cuentas[sujeto].Habilitada);
    }

    [MySqlFact]
    public async Task Administrar_RechazaValoresFueraDeGestionYRolDirectoAjeno()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Administrador, true);
        kc.RolesDirectos[sujeto] = [Roles.Administrador];
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        var servicio = Administracion(kc, servicios);

        await VerificarErrorAsync(ErrorUsuarios.DatosInvalidos, () => servicio.EditarAsync(actor, id,
            Guid.NewGuid(), new("correo-valido@ejemplo.invalid", "realm-admin", true), default));
        await VerificarErrorAsync(ErrorUsuarios.RolFueraDeGestion, () => servicio.EditarAsync(actor, id,
            Guid.NewGuid(), new(kc.Cuentas[sujeto].Correo, Roles.Recepcionista, true), default));
        kc.FueraDeGestion[sujeto] = true;
        await VerificarErrorAsync(ErrorUsuarios.UsuarioNoEncontrado, () =>
            servicio.DesactivarAsync(actor, id, Guid.NewGuid(), default));
        await VerificarErrorAsync(ErrorUsuarios.UsuarioNoEncontrado, () =>
            servicio.DesactivarAsync(actor, int.MaxValue, Guid.NewGuid(), default));
    }

    [MySqlFact]
    public async Task Administrar_ReenvioComparteIntervaloConAltaYUsaSoloAccionesPendientes()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        var altas = new AltaUsuariosServicio(kc, servicios.GetRequiredService<IAltasUsuarios>(), TimeProvider.System,
            servicios.GetRequiredService<IAdministracionUsuarios>());
        var creado = await ExigirExitoAsync(altas.CrearAsync(actor, Guid.NewGuid(),
            Solicitud("invita-" + Guid.NewGuid().ToString("N")), default));
        var servicio = Administracion(kc, servicios);

        var error = await VerificarErrorAsync(ErrorUsuarios.IntervaloInvitacion, () =>
            servicio.ReenviarInvitacionAsync(actor, creado.Id, Guid.NewGuid(), default));
        Assert.InRange(error.SegundosEspera!.Value, 110, 120);

        var cuenta = (await kc.BuscarPorUsernameAsync(creado.Username))!;
        kc.Cuentas[cuenta.Id] = cuenta with { AccionesRequeridas = ["VERIFY_EMAIL"] };
        kc.Contrasenas[cuenta.Id] = true;
        await using var conexion = mysql.CrearConexion();
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            UPDATE ControlInvitacionesUsuarios SET UltimoIntentoUtc = UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE WHERE FichaId = @Id;
            """;
        comando.Parameters.AddWithValue("@Id", creado.Id);
        await comando.ExecuteNonQueryAsync();
        var clave = Guid.NewGuid();
        await ExigirExitoAsync(servicio.ReenviarInvitacionAsync(actor, creado.Id, clave, default));
        await ExigirExitoAsync(servicio.ReenviarInvitacionAsync(actor, creado.Id, clave, default));
        Assert.Equal([AccionInvitacion.VERIFY_EMAIL], kc.UltimasAcciones);
        Assert.Equal(2, kc.SolicitudesCorreo);

        kc.Cuentas[cuenta.Id] = cuenta with { CorreoVerificado = true, AccionesRequeridas = [] };
        await VerificarErrorAsync(ErrorUsuarios.InvitacionNoElegible, () =>
            servicio.ReenviarInvitacionAsync(actor, creado.Id, Guid.NewGuid(), default));
    }

    [MySqlFact]
    public async Task Administrar_EnvioInciertoNoRetornaOtroCorreoAlRepetirClave()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor)
        {
            ErrorCorreo = new("error", ErrorIdentidad.TiempoAgotado, resultadoIncierto: true)
        };
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, false);
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        var servicio = Administracion(kc, servicios);
        var clave = Guid.NewGuid();

        for (int i = 0; i < 2; i++)
        {
            await VerificarErrorAsync(ErrorUsuarios.EnvioInvitacionIncierto,
                () => servicio.ReenviarInvitacionAsync(actor, id, clave, default));
        }
        Assert.Equal(1, kc.SolicitudesCorreo);
        var ficha = await servicios.GetRequiredService<IConsultaFichasUsuarios>().ObtenerAsync(actor.Emisor, id, default);
        Assert.Equal(EstadoAlta.EnvioIncierto, ficha!.Estado);
    }

    [MySqlFact]
    public async Task Administrar_UnaEscrituraInciertaBloqueaOtrasYSeReconciliaPorEstadoReal()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, true);
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        kc.Sesiones[sujeto] = true;
        int peticiones = 0;
        kc.DespuesDeCambiarHabilitacion = (_, _) =>
        {
            peticiones++;
            throw new OperacionIdentidadException("cambio", ErrorIdentidad.TiempoAgotado, resultadoIncierto: true);
        };
        var servicio = Administracion(kc, servicios);
        var clave = Guid.NewGuid();

        await VerificarErrorAsync(ErrorUsuarios.AdministracionPendiente,
            () => servicio.DesactivarAsync(actor, id, clave, default));
        await VerificarErrorAsync(ErrorUsuarios.AdministracionPendiente,
            () => servicio.ReactivarAsync(actor, id, Guid.NewGuid(), default));
        await ExigirExitoAsync(servicio.DesactivarAsync(actor, id, clave, default));

        Assert.Equal(1, peticiones);
        Assert.False(kc.Cuentas[sujeto].Habilitada);
        Assert.Equal(1, kc.SesionesCerradas);
    }

    [MySqlFact]
    public async Task Administrar_ReconciliaFinalizacionSiMySqlFallaDespuesDeKeycloak()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, true);
        kc.Sesiones[sujeto] = true;
        int id = await FichaGestionadaAsync(servicios, actor, sujeto);
        var servicio = Administracion(kc, servicios);
        var clave = Guid.NewGuid();

        await using var conexion = mysql.CrearConexion();
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"""
            ALTER TABLE AuditoriaAdministrativa ADD CONSTRAINT CK_P45_prueba
            CHECK (ActorSujeto <> '{actor.Sujeto}' OR Resultado <> 'Exitoso');
            """;
        await comando.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<MySqlException>(() =>
                servicio.DesactivarAsync(actor, id, clave, default));
            Assert.False(kc.Cuentas[sujeto].Habilitada);
            var ficha = await servicios.GetRequiredService<IFichasAdministrativas>().BuscarPorIdAsync(id);
            Assert.Null(ficha!.FechaModificacionUtc);
        }
        finally
        {
            comando.CommandText = "ALTER TABLE AuditoriaAdministrativa DROP CHECK CK_P45_prueba;";
            await comando.ExecuteNonQueryAsync();
        }

        await ExigirExitoAsync(servicio.DesactivarAsync(actor, id, clave, default));
        Assert.Equal(1, kc.SesionesCerradas);
        var eventos = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        Assert.Single(eventos, e => e.Actor == actor && e.Resultado == ResultadoAuditoria.Exitoso);
    }

    private static AdministracionUsuariosFacade Administracion(KeycloakPrueba kc, IServiceProvider servicios) =>
        new(kc, servicios.GetRequiredService<IFichasAdministrativas>(),
            servicios.GetRequiredService<IAuditoriaAdministrativa>(), servicios.GetRequiredService<IAdministracionUsuarios>(),
            TimeProvider.System);

    private static async Task<string> CuentaGestionadaAsync(KeycloakPrueba kc, string rol, bool verificada)
    {
        string username = "p45-" + Guid.NewGuid().ToString("N");
        string id = await kc.CrearAsync(new NuevaCuentaKeycloak(username, username + "@ejemplo.invalid"));
        await kc.AsignarRolAsync(id, rol);
        if (verificada)
        {
            kc.Cuentas[id] = kc.Cuentas[id] with { CorreoVerificado = true, AccionesRequeridas = [] };
            kc.Contrasenas[id] = true;
        }
        return id;
    }

    private static Task<int> FichaGestionadaAsync(IServiceProvider servicios, IdentidadKeycloak actor, string sujeto) =>
        servicios.GetRequiredService<IFichasAdministrativas>()
            .CrearAsync(new IdentidadKeycloak(actor.Emisor, sujeto), Fecha, actor);

    private static async Task<ErrorResultado> VerificarErrorAsync(ErrorUsuarios esperado, Func<Task<Resultado>> ejecutar)
    {
        var resultado = await ejecutar();
        Assert.False(resultado.EsExitoso);
        Assert.Equal(esperado, resultado.Error!.Codigo);
        return resultado.Error;
    }

    private static async Task VerificarErrorAsync<T>(ErrorUsuarios esperado, Func<Task<Resultado<T>>> ejecutar)
    {
        var resultado = await ejecutar();
        Assert.False(resultado.EsExitoso);
        Assert.Equal(esperado, resultado.Error!.Codigo);
    }

    private static async Task ExigirExitoAsync(Task<Resultado> ejecutar)
    {
        var resultado = await ejecutar;
        Assert.True(resultado.EsExitoso, resultado.Error?.Codigo.ToString());
    }

    private static async Task<T> ExigirExitoAsync<T>(Task<Resultado<T>> ejecutar)
    {
        var resultado = await ejecutar;
        Assert.True(resultado.EsExitoso, resultado.Error?.Codigo.ToString());
        return resultado.Valor;
    }

}
