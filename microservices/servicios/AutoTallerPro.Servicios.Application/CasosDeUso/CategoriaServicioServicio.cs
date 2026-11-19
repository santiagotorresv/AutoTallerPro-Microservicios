using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
using AutoTallerPro.Servicios.Application.Puertos;
namespace AutoTallerPro.Servicios.Application.CasosDeUso;
public sealed class CategoriaServicioServicio(ICategoriaServicioRepositorio repositorio)
{
    public async Task<Resultado<IReadOnlyList<CategoriaServicio>>> ListarAsync(string? buscar, string? ordenar, bool descendente, CancellationToken ct)
    {
        var registros = await repositorio.ListarAsync(ct);
        var filtrados = registros.Where(x => string.IsNullOrWhiteSpace(buscar) ||
            x.Nombre.Contains(buscar.Trim(), StringComparison.OrdinalIgnoreCase));
        Func<CategoriaServicio, IComparable> clave = ordenar?.ToLowerInvariant() switch
        {
            "fecha" => x => x.FechaRegistro,
            "descripcion" => x => x.Descripcion ?? "",
            "nombre" => x => x.Nombre,
            _ => x => x.Nombre
        };
        var ordenados = descendente ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        return Resultado<IReadOnlyList<CategoriaServicio>>.Exito(ordenados.ThenBy(x => x.Id).ToArray());
    }
    public async Task<Resultado<CategoriaServicio>> ObtenerAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return Resultado<CategoriaServicio>.Fallo("categoriaservicio.id", "El identificador no es válido.");
        var entidad = await repositorio.ObtenerAsync(id, ct);
        return entidad is null ? Resultado<CategoriaServicio>.Fallo("categoriaservicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado) : Resultado<CategoriaServicio>.Exito(entidad);
    }
    public async Task<Resultado<CategoriaServicio>> GuardarAsync(Guid? id, DatosCategoriaServicio datos, CancellationToken ct)
    {
        CategoriaServicio? anterior = null;
        if (id.HasValue)
        {
            var consulta = await ObtenerAsync(id.Value, ct);
            if (!consulta.EsExitoso) return consulta;
            anterior = consulta.Valor;
        }
        var resultado = CategoriaServicio.Crear(id ?? Guid.NewGuid(), datos.Nombre, datos.Descripcion, datos.Activo, anterior?.FechaRegistro);
        if (!resultado.EsExitoso) return resultado;
        if (await repositorio.ExisteIdentificadorAsync(resultado.Valor.Nombre, resultado.Valor.Id, ct))
            return Resultado<CategoriaServicio>.Fallo("categoriaservicio.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto);
        return await repositorio.GuardarAsync(resultado.Valor, !id.HasValue, ct);
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        var consulta = await ObtenerAsync(id, ct);
        if (!consulta.EsExitoso) return Resultado<bool>.Fallo(consulta.Error!);
        return await repositorio.EliminarAsync(id, ct);
    }
}
