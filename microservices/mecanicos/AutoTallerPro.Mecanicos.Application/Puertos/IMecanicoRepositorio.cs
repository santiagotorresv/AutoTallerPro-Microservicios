using AutoTallerPro.Mecanicos.Domain;
using AutoTallerPro.Mecanicos.Domain.Resultados;
namespace AutoTallerPro.Mecanicos.Application.Puertos;
public interface IMecanicoRepositorio
{
    Task<IReadOnlyList<Mecanico>> ListarAsync(CancellationToken ct);
    Task<Mecanico?> ObtenerAsync(string id, CancellationToken ct);
    Task<bool> ExisteIdentificadorAsync(string identificador, string exceptoId, CancellationToken ct);
    Task<Resultado<Mecanico>> GuardarAsync(Mecanico entidad, bool nuevo, CancellationToken ct);
    Task<Resultado<bool>> EliminarAsync(string id, CancellationToken ct);

}
