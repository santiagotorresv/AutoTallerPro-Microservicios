using AutoTallerPro.ClientesVehiculos.Application;
using AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
namespace AutoTallerPro.ClientesVehiculos.Api.Adaptadores;
internal static class VehiculoEndpoints
{
    internal static void MapearVehiculo(this WebApplication app)
    {
        var grupo = app.MapGroup("/vehiculos").RequireAuthorization("PersonalAutorizado");
        grupo.MapGet("", async (VehiculoServicio servicio, string? buscar, string? ordenar, bool? descendente, CancellationToken ct) =>
            Respuestas.Http(await servicio.ListarAsync(buscar, ordenar, descendente ?? false, ct)));
        grupo.MapGet("/{id:guid}", async (Guid id, VehiculoServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.ObtenerAsync(id, ct)));
        grupo.MapPost("", async (DatosVehiculo datos, VehiculoServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.GuardarAsync(null, datos, ct);
            return Respuestas.Http(resultado, resultado.EsExitoso ? $"/api/clientes-vehiculos/vehiculos/{resultado.Valor.Id}" : null);
        });
        grupo.MapPut("/{id:guid}", async (Guid id, DatosVehiculo datos, VehiculoServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.GuardarAsync(id, datos, ct)));
        grupo.MapDelete("/{id:guid}", async (Guid id, VehiculoServicio servicio, CancellationToken ct) => Respuestas.Http(await servicio.EliminarAsync(id, ct)));
    }
}
