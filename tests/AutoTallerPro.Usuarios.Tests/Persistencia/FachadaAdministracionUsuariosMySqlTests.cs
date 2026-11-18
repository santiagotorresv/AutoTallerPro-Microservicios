using System.Security.Cryptography;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

public sealed partial class PersistenciaMySqlTests
{
    [MySqlFact]
    public async Task Fachada_DistingueCambiosDeCuentaYReenvioConLaMismaBitacora()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Administrador, true);
        kc.Sesiones[sujeto] = true;
        int ficha = await FichaGestionadaAsync(servicios, actor, sujeto);
        var fachada = Administracion(kc, servicios);
        var claveEdicion = Guid.NewGuid();
        var claveReenvio = Guid.NewGuid();
        string correo = "editada-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";

        await ExigirExitoAsync(fachada.EditarAsync(actor, ficha, claveEdicion,
            new EdicionUsuario(correo, Roles.Recepcionista, true), default));
        Assert.Equal(correo, kc.Cuentas[sujeto].Correo);
        Assert.Equal(1, kc.SesionesCerradas);
        Assert.Equal(0, kc.SolicitudesCorreo);

        await VerificarErrorAsync(ErrorUsuarios.ClaveReutilizada,
            () => fachada.ReenviarInvitacionAsync(actor, ficha, claveEdicion, default));
        await ExigirExitoAsync(fachada.ReenviarInvitacionAsync(actor, ficha, claveReenvio, default));
        await ExigirExitoAsync(fachada.ReenviarInvitacionAsync(actor, ficha, claveReenvio, default));
        Assert.Equal([AccionInvitacion.VERIFY_EMAIL], kc.UltimasAcciones);
        Assert.Equal(1, kc.SolicitudesCorreo);
        Assert.Equal(1, kc.SesionesCerradas);

        var eventos = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        Assert.Equal(2, eventos.Count(e => e.Actor == actor && e.Resultado == ResultadoAuditoria.Pendiente));
        Assert.Equal(["INVITACION_REENVIADA", "USUARIO_EDITADO"], eventos
            .Where(e => e.Actor == actor && e.Resultado == ResultadoAuditoria.Exitoso)
            .Select(e => e.Accion).Order().ToArray());
    }

    [MySqlFact]
    public async Task Fachada_ReconciliaUnaDesactivacionPendienteConLaHuellaAnterior()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, true);
        int ficha = await FichaGestionadaAsync(servicios, actor, sujeto);
        var cambios = CambiosUsuario.Habilitacion | CambiosUsuario.Sesiones;
        var operacion = new OperacionAdministrativa(Guid.NewGuid(), Guid.NewGuid(), actor, ficha,
            TipoAdministracion.Desactivar, HuellaAnterior(activo: false), cambios, cambios,
            CambiosUsuario.Habilitacion, EstadoOperacion.Pendiente);
        await GuardarOperacionAnteriorAsync(servicios, operacion);

        await kc.CambiarHabilitacionAsync(sujeto, false);
        kc.Sesiones[sujeto] = true;
        kc.DespuesDeCambiarHabilitacion = (_, _) => throw new InvalidOperationException("El paso ya se ejecutó.");
        var fachada = Administracion(kc, servicios);

        await ExigirExitoAsync(fachada.DesactivarAsync(actor, ficha, operacion.Clave, default));
        await ExigirExitoAsync(fachada.DesactivarAsync(actor, ficha, operacion.Clave, default));

        Assert.Equal(1, kc.SesionesCerradas);
        var guardada = await LeerOperacionAsync(servicios, operacion);
        Assert.Equal(EstadoOperacion.Exitosa, guardada.Estado);
        Assert.Equal(CambiosUsuario.Ninguno, guardada.Restantes);
    }

    [MySqlFact]
    public async Task Fachada_ReconciliaUnReenvioAnteriorSinMandarOtroCorreo()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        string sujeto = await CuentaGestionadaAsync(kc, Roles.Recepcionista, true);
        int ficha = await FichaGestionadaAsync(servicios, actor, sujeto);
        var operacion = new OperacionAdministrativa(Guid.NewGuid(), Guid.NewGuid(), actor, ficha,
            TipoAdministracion.Reenviar, HuellaAnterior(activo: null), CambiosUsuario.Invitacion,
            CambiosUsuario.Invitacion, CambiosUsuario.Invitacion, EstadoOperacion.Pendiente);
        await GuardarOperacionAnteriorAsync(servicios, operacion);
        var fachada = Administracion(kc, servicios);

        await VerificarErrorAsync(ErrorUsuarios.EnvioInvitacionIncierto,
            () => fachada.ReenviarInvitacionAsync(actor, ficha, operacion.Clave, default));
        await VerificarErrorAsync(ErrorUsuarios.EnvioInvitacionIncierto,
            () => fachada.ReenviarInvitacionAsync(actor, ficha, operacion.Clave, default));

        Assert.Equal(0, kc.SolicitudesCorreo);
        var guardada = await LeerOperacionAsync(servicios, operacion);
        Assert.Equal(EstadoOperacion.EnvioIncierto, guardada.Estado);
        Assert.Equal(ErrorUsuarios.EnvioInvitacionIncierto, guardada.Error);
    }

    private static byte[] HuellaAnterior(bool? activo) =>
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Email = (string?)null, Rol = (string?)null, Activo = activo }));

    private static async Task GuardarOperacionAnteriorAsync(IServiceProvider servicios, OperacionAdministrativa anterior)
    {
        var puerto = servicios.GetRequiredService<IAdministracionUsuarios>();
        await using var sesion = await ExigirExitoAsync(puerto.AbrirAsync(anterior.Actor.Emisor, default));
        await sesion.AbrirOperacionAsync(anterior, Fecha, default);
        if (anterior.Tipo == TipoAdministracion.Reenviar)
        {
            await sesion.ReservarInvitacionAsync(anterior.FichaId, anterior.Id, default);
        }
        await sesion.GuardarPasoAsync(anterior, false, default);
    }

    private static async Task<OperacionAdministrativa> LeerOperacionAsync(IServiceProvider servicios,
        OperacionAdministrativa anterior)
    {
        var puerto = servicios.GetRequiredService<IAdministracionUsuarios>();
        await using var sesion = await ExigirExitoAsync(puerto.AbrirAsync(anterior.Actor.Emisor, default));
        var guardada = await sesion.BuscarAsync(anterior.Clave, default);
        Assert.NotNull(guardada);
        Assert.Equal(anterior.Id, guardada.Id);
        return guardada;
    }
}
