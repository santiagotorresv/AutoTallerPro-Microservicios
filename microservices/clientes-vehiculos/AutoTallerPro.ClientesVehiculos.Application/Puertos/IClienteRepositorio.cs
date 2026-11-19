using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.ClientesVehiculos.Application.Puertos;
public interface IClienteRepositorio
{
    Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct);
    Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct);
    Task<Resultado<Cliente>> GuardarAsync(Cliente entidad, bool nuevo, CancellationToken ct);
    Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct);

}
