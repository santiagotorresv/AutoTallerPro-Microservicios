using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
namespace AutoTallerPro.Servicios.Application.Puertos;
public interface IServicioRepositorio
{
    Task<IReadOnlyList<Servicio>> ListarAsync(CancellationToken ct);
    Task<Servicio?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct);
    Task<Resultado<Servicio>> GuardarAsync(Servicio entidad, bool nuevo, CancellationToken ct);
    Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct);

}
