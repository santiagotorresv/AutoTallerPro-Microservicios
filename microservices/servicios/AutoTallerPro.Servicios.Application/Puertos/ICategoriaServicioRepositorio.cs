using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
namespace AutoTallerPro.Servicios.Application.Puertos;
public interface ICategoriaServicioRepositorio
{
    Task<IReadOnlyList<CategoriaServicio>> ListarAsync(CancellationToken ct);
    Task<CategoriaServicio?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct);
    Task<Resultado<CategoriaServicio>> GuardarAsync(CategoriaServicio entidad, bool nuevo, CancellationToken ct);
    Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct);

}
