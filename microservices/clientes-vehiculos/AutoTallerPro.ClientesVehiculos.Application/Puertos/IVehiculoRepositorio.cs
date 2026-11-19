using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.ClientesVehiculos.Application.Puertos;
public interface IVehiculoRepositorio
{
    Task<IReadOnlyList<Vehiculo>> ListarAsync(CancellationToken ct);
    Task<Vehiculo?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct);
    Task<Resultado<Vehiculo>> GuardarAsync(Vehiculo entidad, bool nuevo, CancellationToken ct);
    Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct);

}
