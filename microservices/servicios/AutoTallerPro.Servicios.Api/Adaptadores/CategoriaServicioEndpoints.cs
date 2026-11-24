using System.Security.Claims;
using AutoTallerPro.Servicios.Application;
using AutoTallerPro.Servicios.Api.Seguridad;
using AutoTallerPro.Servicios.Application.CasosDeUso;
namespace AutoTallerPro.Servicios.Api.Adaptadores;
internal static class CategoriaServicioEndpoints
{
    internal static void MapearCategoriaServicio(this WebApplication app)
    {
        var grupo = app.MapGroup("/categorias").RequireAuthorization("PersonalAutorizado");
        grupo.MapGet("", async (CategoriaServicioServicio servicio, string? buscar, string? ordenar, bool? descendente, CancellationToken ct) =>
            Respuestas.Http(await servicio.ListarAsync(buscar, ordenar, descendente ?? false, ct)));
        grupo.MapGet("/{id:guid}", async (Guid id, CategoriaServicioServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.ObtenerAsync(id, ct)));
        grupo.MapPost("", async (DatosCategoriaServicio datos, CategoriaServicioServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(null, datos, ct, Actor.De(usuario));
            return Respuestas.Http(resultado, resultado.EsExitoso ? $"/api/servicios/categorias/{resultado.Valor.Id}" : null);
        });
        grupo.MapPut("/{id:guid}", async (Guid id, DatosCategoriaServicio datos, CategoriaServicioServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) => Respuestas.Http(await servicio.GuardarAsync(id, datos, ct, Actor.De(usuario))));
        grupo.MapDelete("/{id:guid}", async (Guid id, CategoriaServicioServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.EliminarAsync(id, ct)));
    }
}
