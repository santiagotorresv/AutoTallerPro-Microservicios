using AutoTallerPro.Mecanicos.Domain;
using AutoTallerPro.Mecanicos.Domain.Resultados;
using AutoTallerPro.Mecanicos.Application.Puertos;
namespace AutoTallerPro.GrupoUno.Tests;
internal sealed class MecanicoMemoria : IMecanicoRepositorio
{
    internal readonly Dictionary<string, Mecanico> Datos = [];
    internal int Escrituras;
    public Task<IReadOnlyList<Mecanico>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Mecanico>>(Datos.Values.ToArray());
    public Task<Mecanico?> ObtenerAsync(string id, CancellationToken ct) => Task.FromResult(Datos.GetValueOrDefault(id));
    public Task<bool> ExisteIdentificadorAsync(string identificador, string exceptoId, CancellationToken ct) => Task.FromResult(Datos.Values.Any(x => x.Id != exceptoId && x.Documento.Equals(identificador, StringComparison.OrdinalIgnoreCase)));
    public Task<Resultado<Mecanico>> GuardarAsync(Mecanico entidad, bool nuevo, CancellationToken ct) { Datos[entidad.Id] = entidad; Escrituras++; return Task.FromResult(Resultado<Mecanico>.Exito(entidad)); }
    public Task<Resultado<bool>> EliminarAsync(string id, CancellationToken ct)
    {

        return Task.FromResult(Resultado<bool>.Exito(Datos.Remove(id)));
    }

}
