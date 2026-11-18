using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

[Trait("Categoria", "IntegracionMySql")]
public sealed partial class PersistenciaMySqlTests(MySqlUsuariosAislado mysql) : IClassFixture<MySqlUsuariosAislado>
{
    private const string Emisor = "http://127.0.0.1:8180/realms/autotaller";
    private static readonly DateTimeOffset Fecha =
        new DateTimeOffset(2026, 5, 20, 12, 30, 45, TimeSpan.FromHours(-4)).AddTicks(1234560);
    private static readonly IdentidadKeycloak Actor = new(Emisor, "actor-de-prueba");

    [MySqlFact]
    public async Task Crear_DevuelveIdYPermiteConsultarPorIdEIdentidad()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var fichas = alcance.ServiceProvider.GetRequiredService<IFichasAdministrativas>();
        var identidad = IdentidadNueva();

        int id = await fichas.CrearAsync(identidad, Fecha, Actor);
        var porId = await fichas.BuscarPorIdAsync(id);
        var porIdentidad = await fichas.BuscarPorIdentidadAsync(identidad);

        Assert.True(id > 0);
        Assert.Equal(new FichaAdministrativa(id, identidad, Fecha.ToUniversalTime(), Actor, null, null), porId);
        Assert.Equal(porId, porIdentidad);
        Assert.Equal(TimeSpan.Zero, porId!.FechaCreacionUtc.Offset);
        Assert.Null(await fichas.BuscarPorIdAsync(int.MaxValue));
        Assert.Null(await fichas.BuscarPorIdentidadAsync(IdentidadNueva()));
    }

    [MySqlFact]
    public async Task Crear_ExigeVinculoUnicoYRespetaElEmisorYLetrasExactas()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var fichas = alcance.ServiceProvider.GetRequiredService<IFichasAdministrativas>();
        var identidad = IdentidadNueva();
        int id = await fichas.CrearAsync(identidad, Fecha, null);

        var conflicto = await Assert.ThrowsAsync<IdentidadYaVinculadaException>(() =>
            fichas.CrearAsync(identidad, Fecha, Actor));
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, Assert.IsType<MySqlException>(conflicto.InnerException).ErrorCode);
        Assert.Equal(id, (await fichas.BuscarPorIdentidadAsync(identidad))!.Id);
        Assert.Null((await fichas.BuscarPorIdAsync(id))!.CreadoPor);

        var otroEmisor = new IdentidadKeycloak("http://127.0.0.1:8180/realms/otro", identidad.Sujeto);
        var otrasMayusculas = new IdentidadKeycloak(identidad.Emisor, identidad.Sujeto.ToUpperInvariant());
        Assert.NotEqual(id, await fichas.CrearAsync(otroEmisor, Fecha, null));
        Assert.NotEqual(id, await fichas.CrearAsync(otrasMayusculas, Fecha, null));
    }

    [MySqlFact]
    public async Task ActualizarMetadatos_RespetaIdentidadYDatosDeCreacion()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var fichas = alcance.ServiceProvider.GetRequiredService<IFichasAdministrativas>();
        var identidad = IdentidadNueva();
        var editor = IdentidadNueva();
        int id = await fichas.CrearAsync(identidad, Fecha, Actor);
        var modificacion = Fecha.AddHours(1).ToOffset(TimeSpan.FromHours(3));

        Assert.True(await fichas.ActualizarMetadatosAsync(id, modificacion, editor));
        Assert.True(await fichas.ActualizarMetadatosAsync(id, modificacion, editor));
        Assert.False(await fichas.ActualizarMetadatosAsync(int.MaxValue, modificacion, editor));
        var ficha = await fichas.BuscarPorIdAsync(id);
        Assert.Equal(new FichaAdministrativa(id, identidad, Fecha.ToUniversalTime(), Actor,
            modificacion.ToUniversalTime(), editor), ficha);
        Assert.Equal(TimeSpan.Zero, ficha!.FechaModificacionUtc!.Value.Offset);
    }

    [MySqlFact]
    public async Task ActualizarMetadatos_RechazaFechaAnteriorALaCreacion()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var fichas = alcance.ServiceProvider.GetRequiredService<IFichasAdministrativas>();
        int id = await fichas.CrearAsync(IdentidadNueva(), Fecha, Actor);

        var error = await Assert.ThrowsAsync<MySqlException>(() =>
            fichas.ActualizarMetadatosAsync(id, Fecha.AddDays(-1), Actor));

        Assert.Equal(3819, error.Number);
        Assert.Null((await fichas.BuscarPorIdAsync(id))!.FechaModificacionUtc);
    }

    [MySqlFact]
    public async Task Auditoria_ConservaActorObjetivoResultadoYMetadatosUtc()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var auditoria = alcance.ServiceProvider.GetRequiredService<IAuditoriaAdministrativa>();
        var nuevo = Evento("USUARIO_EDITADO", Fecha, ResultadoAuditoria.Pendiente, "CAMBIO_PENDIENTE", "corr-pruebas");
        long id = await auditoria.RegistrarAsync(nuevo);

        var guardado = Assert.Single(await auditoria.ObtenerRecientesAsync(500), evento => evento.Id == id);

        Assert.True(id > 0);
        Assert.Equal(nuevo with { Id = id, FechaUtc = Fecha.ToUniversalTime() }, guardado);
        Assert.Equal(TimeSpan.Zero, guardado.FechaUtc.Offset);
    }

    [MySqlFact]
    public async Task Auditoria_OrdenaPorFechaEIdYRespetaElLimite()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var auditoria = alcance.ServiceProvider.GetRequiredService<IAuditoriaAdministrativa>();
        var fecha = Fecha.AddYears(40);
        long primero = await auditoria.RegistrarAsync(Evento("PRIMERO", fecha, ResultadoAuditoria.Exitoso));
        long segundo = await auditoria.RegistrarAsync(Evento("SEGUNDO", fecha, ResultadoAuditoria.Fallido));
        await auditoria.RegistrarAsync(Evento("ANTERIOR", fecha.AddDays(-1), ResultadoAuditoria.Exitoso));

        var recientes = await auditoria.ObtenerRecientesAsync(2);

        Assert.Equal(new[] { segundo, primero }, recientes.Select(evento => evento.Id));
        Assert.All(recientes, evento =>
        {
            Assert.Null(evento.CodigoMotivo);
            Assert.Null(evento.CorrelacionId);
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => auditoria.ObtenerRecientesAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => auditoria.ObtenerRecientesAsync(501));
    }

    [MySqlFact]
    public async Task Auditoria_RechazaMotivoLibreYResultadoFueraDelCatalogo()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var auditoria = alcance.ServiceProvider.GetRequiredService<IAuditoriaAdministrativa>();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            auditoria.RegistrarAsync(Evento("EDICION", Fecha, ResultadoAuditoria.Fallido, "texto libre")));
        var error = await Assert.ThrowsAsync<MySqlException>(() =>
            auditoria.RegistrarAsync(Evento("EDICION", Fecha, (ResultadoAuditoria)42)));

        Assert.Equal(3819, error.Number);
    }

    [MySqlFact]
    public async Task ReinicioDeApiYReaplicacionDelEsquema_ConservanFichaYEvento()
    {
        var identidad = IdentidadNueva();
        int idFicha;
        long idEvento;
        await using (var api = mysql.CrearApi())
        {
            using var alcance = api.Services.CreateScope();
            var servicios = alcance.ServiceProvider;
            idFicha = await servicios.GetRequiredService<IFichasAdministrativas>().CrearAsync(identidad, Fecha, Actor);
            idEvento = await servicios.GetRequiredService<IAuditoriaAdministrativa>()
                .RegistrarAsync(Evento("CREACION", Fecha, ResultadoAuditoria.Exitoso));
        }

        await mysql.AplicarEsquemaAsync();
        await using var apiReiniciada = mysql.CrearApi();
        using var alcanceNuevo = apiReiniciada.Services.CreateScope();
        var ficha = await alcanceNuevo.ServiceProvider.GetRequiredService<IFichasAdministrativas>()
            .BuscarPorIdentidadAsync(identidad);
        var eventos = await alcanceNuevo.ServiceProvider.GetRequiredService<IAuditoriaAdministrativa>()
            .ObtenerRecientesAsync(500);

        Assert.Equal(idFicha, ficha!.Id);
        Assert.Contains(eventos, evento => evento.Id == idEvento);
    }

    [MySqlFact]
    public async Task ErrorDeConexion_SeDistingueDeUnaFichaInexistente()
    {
        await using var api = mysql.CrearApi(Guid.NewGuid().ToString("N"));
        using var alcance = api.Services.CreateScope();
        var fichas = alcance.ServiceProvider.GetRequiredService<IFichasAdministrativas>();

        var error = await Assert.ThrowsAsync<MySqlException>(() => fichas.BuscarPorIdAsync(1));

        Assert.Equal(MySqlErrorCode.AccessDenied, error.ErrorCode);
    }

    private static IdentidadKeycloak IdentidadNueva() => new(Emisor, "sujeto-" + Guid.NewGuid().ToString("N"));

    private static RegistroAuditoria Evento(
        string accion, DateTimeOffset fecha, ResultadoAuditoria resultado,
        string? motivo = null, string? correlacion = null) =>
        new(Actor, accion, "Usuario", "ficha-de-prueba", fecha, resultado, motivo, correlacion);
}
