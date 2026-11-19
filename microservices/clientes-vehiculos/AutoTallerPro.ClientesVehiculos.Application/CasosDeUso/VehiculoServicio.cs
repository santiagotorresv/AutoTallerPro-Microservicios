using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
public sealed class VehiculoServicio(IVehiculoRepositorio repositorio, IClienteRepositorio relacionados)
{
    public async Task<Resultado<IReadOnlyList<Vehiculo>>> ListarAsync(string? buscar, string? ordenar, bool descendente, CancellationToken ct)
    {
        var registros = await repositorio.ListarAsync(ct);
        var filtrados = registros.Where(x => string.IsNullOrWhiteSpace(buscar) ||
            x.Placa.Contains(buscar.Trim(), StringComparison.OrdinalIgnoreCase));
        Func<Vehiculo, IComparable> clave = ordenar?.ToLowerInvariant() switch
        {
            "fecha" => x => x.FechaRegistro,
            "marca" => x => x.Marca,
            "modelo" => x => x.Modelo,
            "anio" => x => x.Anio,
            "placa" => x => x.Placa,
            _ => x => x.Placa
        };
        var ordenados = descendente ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        return Resultado<IReadOnlyList<Vehiculo>>.Exito(ordenados.ThenBy(x => x.Id).ToArray());
    }
    public async Task<Resultado<Vehiculo>> ObtenerAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return Resultado<Vehiculo>.Fallo("vehiculo.id", "El identificador no es válido.");
        var entidad = await repositorio.ObtenerAsync(id, ct);
        return entidad is null ? Resultado<Vehiculo>.Fallo("vehiculo.ausente", "No se encontró el registro.", TipoError.NoEncontrado) : Resultado<Vehiculo>.Exito(entidad);
    }
    public async Task<Resultado<Vehiculo>> GuardarAsync(Guid? id, DatosVehiculo datos, CancellationToken ct)
    {
        Vehiculo? anterior = null;
        if (id.HasValue)
        {
            var consulta = await ObtenerAsync(id.Value, ct);
            if (!consulta.EsExitoso) return consulta;
            anterior = consulta.Valor;
        }
        var resultado = Vehiculo.Crear(id ?? Guid.NewGuid(), datos.ClienteId, datos.Placa, datos.Marca, datos.Modelo, datos.Anio, datos.Color, datos.Activo, anterior?.FechaRegistro);
        if (!resultado.EsExitoso) return resultado;
        var relacionado = await relacionados.ObtenerAsync(datos.ClienteId, ct);
        if (relacionado is null) return Resultado<Vehiculo>.Fallo("vehiculo.relacion", "No se encontró el registro relacionado.", TipoError.NoEncontrado);
        if (!relacionado.Activo) return Resultado<Vehiculo>.Fallo("vehiculo.inactivo", "El registro relacionado está inactivo.", TipoError.Conflicto);
        if (await repositorio.ExisteIdentificadorAsync(resultado.Valor.Placa, resultado.Valor.Id, ct))
            return Resultado<Vehiculo>.Fallo("vehiculo.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto);
        return await repositorio.GuardarAsync(resultado.Valor, !id.HasValue, ct);
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        var consulta = await ObtenerAsync(id, ct);
        if (!consulta.EsExitoso) return Resultado<bool>.Fallo(consulta.Error!);
        return await repositorio.EliminarAsync(id, ct);
    }
}
