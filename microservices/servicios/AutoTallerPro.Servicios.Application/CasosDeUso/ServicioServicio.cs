using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
using AutoTallerPro.Servicios.Application.Puertos;
namespace AutoTallerPro.Servicios.Application.CasosDeUso;
public sealed class ServicioServicio(IServicioRepositorio repositorio, ICategoriaServicioRepositorio relacionados)
{
    public async Task<Resultado<IReadOnlyList<Servicio>>> ListarAsync(string? buscar, string? ordenar, bool descendente, CancellationToken ct)
    {
        var registros = await repositorio.ListarAsync(ct);
        var filtrados = registros.Where(x => string.IsNullOrWhiteSpace(buscar) ||
            x.Nombre.Contains(buscar.Trim(), StringComparison.OrdinalIgnoreCase));
        Func<Servicio, IComparable> clave = ordenar?.ToLowerInvariant() switch
        {
            "fecha" => x => x.FechaRegistro,
            "preciobase" => x => x.PrecioBase,
            "duracionestimada" => x => x.DuracionEstimada ?? 0,
            "nombre" => x => x.Nombre,
            _ => x => x.Nombre
        };
        var ordenados = descendente ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        return Resultado<IReadOnlyList<Servicio>>.Exito(ordenados.ThenBy(x => x.Id).ToArray());
    }
    public async Task<Resultado<Servicio>> ObtenerAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return Resultado<Servicio>.Fallo("servicio.id", "El identificador no es válido.");
        var entidad = await repositorio.ObtenerAsync(id, ct);
        return entidad is null ? Resultado<Servicio>.Fallo("servicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado) : Resultado<Servicio>.Exito(entidad);
    }
    public async Task<Resultado<Servicio>> GuardarAsync(Guid? id, DatosServicio datos, CancellationToken ct)
    {
        Servicio? anterior = null;
        if (id.HasValue)
        {
            var consulta = await ObtenerAsync(id.Value, ct);
            if (!consulta.EsExitoso) return consulta;
            anterior = consulta.Valor;
        }
        var resultado = Servicio.Crear(id ?? Guid.NewGuid(), datos.CategoriaServicioId, datos.Nombre, datos.Descripcion, datos.PrecioBase, datos.DuracionEstimada, datos.Activo, anterior?.FechaRegistro);
        if (!resultado.EsExitoso) return resultado;
        var relacionado = await relacionados.ObtenerAsync(datos.CategoriaServicioId, ct);
        if (relacionado is null) return Resultado<Servicio>.Fallo("servicio.relacion", "No se encontró el registro relacionado.", TipoError.NoEncontrado);
        if (!relacionado.Activo) return Resultado<Servicio>.Fallo("servicio.inactivo", "El registro relacionado está inactivo.", TipoError.Conflicto);
        if (await repositorio.ExisteIdentificadorAsync(resultado.Valor.Nombre, resultado.Valor.Id, ct))
            return Resultado<Servicio>.Fallo("servicio.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto);
        return await repositorio.GuardarAsync(resultado.Valor, !id.HasValue, ct);
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        var consulta = await ObtenerAsync(id, ct);
        if (!consulta.EsExitoso) return Resultado<bool>.Fallo(consulta.Error!);
        return await repositorio.EliminarAsync(id, ct);
    }
}
