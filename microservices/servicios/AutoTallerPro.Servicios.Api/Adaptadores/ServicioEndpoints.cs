using System.Security.Claims;
using AutoTallerPro.Servicios.Application;
using AutoTallerPro.Servicios.Api.Seguridad;
using AutoTallerPro.Servicios.Application.CasosDeUso;
namespace AutoTallerPro.Servicios.Api.Adaptadores;
internal static class ServicioEndpoints
{
    internal static void MapearServicio(this WebApplication app)
    {
        var grupo = app.MapGroup("/servicios").RequireAuthorization("PersonalAutorizado");
        grupo.MapGet("", async (ServicioServicio servicio, string? buscar, string? ordenar, bool? descendente, CancellationToken ct) =>
            Respuestas.Http(await servicio.ListarAsync(buscar, ordenar, descendente ?? false, ct)));
        grupo.MapGet("/{id:guid}", async (Guid id, ServicioServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.ObtenerAsync(id, ct)));
        grupo.MapPost("", async (DatosServicio datos, ServicioServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(null, datos, ct, Actor.De(usuario));
            return Respuestas.Http(resultado, resultado.EsExitoso ? $"/api/servicios/servicios/{resultado.Valor.Id}" : null);
        });
        grupo.MapPut("/{id:guid}", async (Guid id, DatosServicio datos, ServicioServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) => Respuestas.Http(await servicio.GuardarAsync(id, datos, ct, Actor.De(usuario))));
        grupo.MapDelete("/{id:guid}", async (Guid id, ServicioServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.EliminarAsync(id, ct)));
    }
}
