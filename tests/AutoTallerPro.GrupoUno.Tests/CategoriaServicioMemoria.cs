using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
using AutoTallerPro.Servicios.Application.Puertos;
namespace AutoTallerPro.GrupoUno.Tests;
internal sealed class CategoriaServicioMemoria : ICategoriaServicioRepositorio
{
    internal readonly Dictionary<Guid, CategoriaServicio> Datos = [];
    internal int Escrituras;
    public Task<IReadOnlyList<CategoriaServicio>> ListarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CategoriaServicio>>(Datos.Values.ToArray());
    public Task<CategoriaServicio?> ObtenerAsync(Guid id, CancellationToken ct) => Task.FromResult(Datos.GetValueOrDefault(id));
    public Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct) => Task.FromResult(Datos.Values.Any(x => x.Id != exceptoId && x.Nombre.Equals(identificador, StringComparison.OrdinalIgnoreCase)));
    public Task<Resultado<CategoriaServicio>> GuardarAsync(CategoriaServicio entidad, bool nuevo, CancellationToken ct) { Datos[entidad.Id] = entidad; Escrituras++; return Task.FromResult(Resultado<CategoriaServicio>.Exito(entidad)); }
    public Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {

        return Task.FromResult(Resultado<bool>.Exito(Datos.Remove(id)));
    }

}
