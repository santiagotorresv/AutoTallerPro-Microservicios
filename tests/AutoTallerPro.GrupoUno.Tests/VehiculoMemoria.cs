using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.GrupoUno.Tests;
internal sealed class VehiculoMemoria : IVehiculoRepositorio
{
    internal readonly Dictionary<Guid, Vehiculo> Datos = [];
    internal int Escrituras;
    public Task<IReadOnlyList<Vehiculo>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Vehiculo>>(Datos.Values.ToArray());
    public Task<Vehiculo?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Datos.GetValueOrDefault(id));
    public Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct) => Task.FromResult(Datos.Values.Any(x => x.Id != exceptoId && x.Placa.Equals(identificador, StringComparison.OrdinalIgnoreCase)));
    public Task<Resultado<Vehiculo>> GuardarAsync(Vehiculo entidad, bool nuevo, CancellationToken ct) { Datos[entidad.Id] = entidad; Escrituras++; return Task.FromResult(Resultado<Vehiculo>.Exito(entidad)); }
    public Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {

        return Task.FromResult(Resultado<bool>.Exito(Datos.Remove(id)));
    }

}
