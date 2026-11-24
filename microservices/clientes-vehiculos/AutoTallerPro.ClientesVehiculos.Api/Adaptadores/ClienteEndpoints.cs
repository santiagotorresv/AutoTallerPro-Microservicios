using System.Security.Claims;
using AutoTallerPro.ClientesVehiculos.Application;
using AutoTallerPro.ClientesVehiculos.Api.Seguridad;
using AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
namespace AutoTallerPro.ClientesVehiculos.Api.Adaptadores;
internal static class ClienteEndpoints
{
    internal static void MapearCliente(this WebApplication app)
    {
        var grupo = app.MapGroup("/clientes").RequireAuthorization("PersonalAutorizado");
        grupo.MapGet("", async (ClienteServicio servicio, string? buscar, string? ordenar, bool? descendente, CancellationToken ct) =>
            Respuestas.Http(await servicio.ListarAsync(buscar, ordenar, descendente ?? false, ct)));
        grupo.MapGet("/{id:guid}", async (Guid id, ClienteServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.ObtenerAsync(id, ct)));
        grupo.MapPost("", async (DatosCliente datos, ClienteServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(null, datos, ct, Actor.De(usuario));
            return Respuestas.Http(resultado, resultado.EsExitoso ? $"/api/clientes-vehiculos/clientes/{resultado.Valor.Id}" : null);
        });
        grupo.MapPut("/{id:guid}", async (Guid id, DatosCliente datos, ClienteServicio servicio, ClaimsPrincipal usuario, CancellationToken ct) => Respuestas.Http(await servicio.GuardarAsync(id, datos, ct, Actor.De(usuario))));
        grupo.MapDelete("/{id:guid}", async (Guid id, ClienteServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.EliminarAsync(id, ct)));
    }
}
