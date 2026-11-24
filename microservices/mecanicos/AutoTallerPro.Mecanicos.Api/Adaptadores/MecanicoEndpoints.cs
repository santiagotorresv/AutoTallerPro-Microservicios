using System.Security.Claims;
using AutoTallerPro.Mecanicos.Application;
using AutoTallerPro.Mecanicos.Api.Seguridad;
using AutoTallerPro.Mecanicos.Application.CasosDeUso;
namespace AutoTallerPro.Mecanicos.Api.Adaptadores;
internal static class MecanicoEndpoints
{
    internal static void MapearMecanico(this WebApplication app)
    {
        var grupo = app.MapGroup("/mecanicos").RequireAuthorization("PersonalAutorizado");
        grupo.MapGet("", async (MecanicoServicio servicio, string? buscar, string? ordenar, bool? descendente, CancellationToken ct) =>
            Respuestas.Http(await servicio.ListarAsync(buscar, ordenar, descendente ?? false, ct)));
        grupo.MapGet("/{id}", async (string id, MecanicoServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.ObtenerAsync(id, ct)));
        grupo.MapPost("", async (DatosMecanico datos, MecanicoServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(null, datos, ct, Actor.De(usuario));
            return Respuestas.Http(resultado, resultado.EsExitoso ? $"/api/mecanicos/mecanicos/{resultado.Valor.Id}" : null);
        });
        grupo.MapPut("/{id}", async (string id, DatosMecanico datos, MecanicoServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) => Respuestas.Http(await servicio.GuardarAsync(id, datos, ct, Actor.De(usuario))));
        grupo.MapDelete("/{id}", async (string id, MecanicoServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.EliminarAsync(id, ct)));
    }
}
