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
    public async Task Aprovisionamiento_ExigeQueElEmisorCoincidaConElRealmAdministrado()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = KeycloakVacio(actor);
        kc.Emisor += "/otro";
        var servicio = Servicio(kc, servicios);
        var fichas = servicios.GetRequiredService<IConsultaFichasUsuarios>();

        await VerificarErrorAsync(ErrorUsuarios.EmisorNoCoincide, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(), "nuevo@ejemplo.invalid",
                "Admin", "Inicial", fichas, default));
        Assert.Empty(kc.Cuentas);
        Assert.Empty(await fichas.ListarAsync(actor.Emisor, default));
    }

    [MySqlFact]
    public async Task Aprovisionamiento_CreaFichaAuditoriaEInvitacionYNoPermiteSustituirElPrimerAdmin()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = KeycloakVacio(actor);
        var servicio = Servicio(kc, servicios);
        var fichas = servicios.GetRequiredService<IConsultaFichasUsuarios>();
        var clave = Guid.NewGuid();
        string correo = "bootstrap-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";

        var primero = await ExigirExitoAsync(servicio.AprovisionarPrimeroAsync(
            actor.Emisor, clave, correo, "Admin", "Inicial", fichas, default));
        Assert.Equal(primero, await ExigirExitoAsync(servicio.AprovisionarPrimeroAsync(
            actor.Emisor, clave, correo, "Admin", "Inicial", fichas, default)));
        await VerificarErrorAsync(ErrorUsuarios.ClaveReutilizada, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, clave, correo, "Otro", "Inicial", fichas, default));
        await VerificarErrorAsync(ErrorUsuarios.PrimerAdministradorNoDisponible, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(), correo, "Admin", "Inicial", fichas, default));

        var cuenta = Assert.Single(kc.Cuentas.Values);
        Assert.False(await kc.TieneContrasenaAsync(cuenta.Id));
        Assert.Equal([Roles.Administrador], await kc.ObtenerRolesAsync(cuenta.Id));
        Assert.Equal(1, kc.Altas);
        Assert.Equal(1, kc.SolicitudesCorreo);
        Assert.Equal("correoSolicitado", primero.EstadoInvitacion);
        var ficha = Assert.Single(await fichas.ListarAsync(actor.Emisor, default));
        Assert.Equal(primero.Id, ficha.Id);
        Assert.Equal(cuenta.Id, ficha.Sujeto);

        var auditoria = await servicios.GetRequiredService<IAuditoriaAdministrativa>().ObtenerRecientesAsync(500);
        var evento = Assert.Single(auditoria, e => e.Actor.Emisor == actor.Emisor);
        Assert.Equal(AltaUsuariosServicio.ActorAprovisionamiento, evento.Actor.Sujeto);
        Assert.Equal("PRIMER_ADMINISTRADOR_CREADO", evento.Accion);
    }

    [MySqlFact]
    public async Task Aprovisionamiento_NoAdoptaUnaCuentaExistenteNiUnRealmConFichas()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor);
        var fichas = servicios.GetRequiredService<IConsultaFichasUsuarios>();
        var servicio = Servicio(kc, servicios);
        string correo = "preexistente-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";
        kc.Cuentas[actor.Sujeto] = kc.Cuentas[actor.Sujeto] with { Correo = correo };

        await VerificarErrorAsync(ErrorUsuarios.PrimerAdministradorNoDisponible, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(), correo, "Admin", "Inicial", fichas, default));
        Assert.Empty(await fichas.ListarAsync(actor.Emisor, default));

        await FichaGestionadaAsync(servicios, actor, actor.Sujeto);
        Assert.True(kc.Cuentas.TryRemove(actor.Sujeto, out _));
        await VerificarErrorAsync(ErrorUsuarios.PrimerAdministradorNoDisponible, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(), correo, "Admin", "Inicial", fichas, default));

        Assert.Equal(0, kc.Altas);
        Assert.Equal(0, kc.SolicitudesCorreo);
    }

    [MySqlFact]
    public async Task Aprovisionamiento_ReconciliaLaCreacionInciertaConLaMismaClave()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = KeycloakVacio(actor, timeout: true);
        var servicio = Servicio(kc, servicios);
        var fichas = servicios.GetRequiredService<IConsultaFichasUsuarios>();
        var clave = Guid.NewGuid();
        string correo = "incierto-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";

        var incierto = await Assert.ThrowsAsync<OperacionIdentidadException>(() =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, clave, correo, "Admin", "Inicial", fichas, default));
        Assert.True(incierto.ResultadoIncierto);
        await VerificarErrorAsync(ErrorUsuarios.PrimerAdministradorNoDisponible, () =>
            servicio.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(), correo, "Admin", "Inicial", fichas, default));
        var recuperado = await ExigirExitoAsync(servicio.AprovisionarPrimeroAsync(
            actor.Emisor, clave, correo, "Admin", "Inicial", fichas, default));

        Assert.Equal(1, kc.Altas);
        Assert.Equal(1, kc.SolicitudesCorreo);
        Assert.Equal(recuperado.Id, Assert.Single(await fichas.ListarAsync(actor.Emisor, default)).Id);
    }

    [MySqlFact]
    public async Task Aprovisionamiento_ComparteElBloqueoEntreDosInstancias()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        using var segundoAlcance = mysql.Api.Services.CreateScope();
        var actor = ActorAlta();
        var kc = KeycloakVacio(actor);
        var servicios = alcance.ServiceProvider;
        var otros = segundoAlcance.ServiceProvider;
        var primera = Servicio(kc, servicios);
        var segunda = Servicio(kc, otros);
        var fichas = servicios.GetRequiredService<IConsultaFichasUsuarios>();
        string prefijo = Guid.NewGuid().ToString("N");
        var empezar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seguir = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kc.AntesDeCrear = async _ =>
        {
            empezar.TrySetResult();
            await seguir.Task.WaitAsync(TimeSpan.FromSeconds(8));
        };

        var solicitud = primera.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(),
            prefijo + "@ejemplo.invalid", "Admin", "Primero", fichas, default);
        await empezar.Task.WaitAsync(TimeSpan.FromSeconds(8));
        var alternativa = segunda.AprovisionarPrimeroAsync(actor.Emisor, Guid.NewGuid(),
            prefijo + "@otro.invalid", "Admin", "Segundo", otros.GetRequiredService<IConsultaFichasUsuarios>(), default);
        seguir.TrySetResult();

        await ExigirExitoAsync(solicitud);
        await VerificarErrorAsync(ErrorUsuarios.PrimerAdministradorNoDisponible, () => alternativa);
        Assert.Equal(1, kc.Altas);
        Assert.Single(await fichas.ListarAsync(actor.Emisor, default));
    }

    [MySqlFact]
    public async Task Alta_NoReenviaInvitacionOriginalUnaVezCumplidasLasAcciones()
    {
        using var alcance = mysql.Api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var actor = ActorAlta();
        var kc = new KeycloakPrueba(actor) { ErrorCorreo = new("correo", ErrorIdentidad.Prohibido, 403) };
        var servicio = Servicio(kc, servicios);
        var clave = Guid.NewGuid();
        var datos = Solicitud("completada-" + Guid.NewGuid().ToString("N"));

        var creada = await ExigirExitoAsync(servicio.CrearAsync(actor, clave, datos, default));
        var cuenta = (await kc.BuscarPorUsernameAsync(creada.Username))!;
        kc.Cuentas[cuenta.Id] = cuenta with { CorreoVerificado = true, AccionesRequeridas = [] };
        kc.Contrasenas[cuenta.Id] = true;
        kc.ErrorCorreo = null;
        await ExigirExitoAsync(servicio.CrearAsync(actor, clave, datos, default));

        Assert.Equal(1, kc.SolicitudesCorreo);
        var consulta = new ConsultaUsuariosServicio(servicios.GetRequiredService<IConsultaFichasUsuarios>(), kc);
        Assert.Equal("completada", (await ExigirExitoAsync(consulta.DetalleAsync(actor.Emisor, creada.Id, default))).EstadoInvitacion);
    }

    private static KeycloakPrueba KeycloakVacio(IdentidadKeycloak actor, bool timeout = false)
    {
        var kc = new KeycloakPrueba(actor) { TimeoutAlCrear = timeout };
        Assert.True(kc.Cuentas.TryRemove(actor.Sujeto, out _));
        return kc;
    }
}
