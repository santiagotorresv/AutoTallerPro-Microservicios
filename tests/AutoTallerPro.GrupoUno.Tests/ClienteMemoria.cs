using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.GrupoUno.Tests;
internal sealed class ClienteMemoria : IClienteRepositorio
{
    internal readonly Dictionary<Guid, Cliente> Datos = [];
    internal int Escrituras;
    public Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Cliente>>(Datos.Values.ToArray());
    public Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Datos.GetValueOrDefault(id));
    public Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct) => Task.FromResult(Datos.Values.Any(x => x.Id != exceptoId && x.Documento.Equals(identificador, StringComparison.OrdinalIgnoreCase)));
    public Task<Resultado<Cliente>> GuardarAsync(Cliente entidad, bool nuevo, CancellationToken ct) { Datos[entidad.Id] = entidad; Escrituras++; return Task.FromResult(Resultado<Cliente>.Exito(entidad)); }
    public Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {

        return Task.FromResult(Resultado<bool>.Exito(Datos.Remove(id)));
    }

}
