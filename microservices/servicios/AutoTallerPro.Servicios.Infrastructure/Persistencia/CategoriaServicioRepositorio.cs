using MySqlConnector;
using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
using AutoTallerPro.Servicios.Application.Puertos;
namespace AutoTallerPro.Servicios.Infrastructure.Persistencia;
public sealed class CategoriaServicioRepositorio(Conexion conexiones) : ICategoriaServicioRepositorio
{
    private static CategoriaServicio Leer(System.Data.Common.DbDataReader lector) => CategoriaServicio.Crear(Guid.Parse(Convert.ToString(lector["id"])!), (string)lector["nombre"], Sql.Texto(lector, "descripcion"), Convert.ToBoolean(lector["activo"]), Sql.Fecha(lector, "fecharegistro"), Sql.Fecha(lector, "fechaactualizacion")).Valor;
    public async Task<IReadOnlyList<CategoriaServicio>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT id, nombre, descripcion, activo, fecharegistro, fechaactualizacion FROM categorias_servicio");
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var entidades = new List<CategoriaServicio>(); while (await lector.ReadAsync(ct)) entidades.Add(Leer(lector));
        return entidades;
    }
    public async Task<CategoriaServicio?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT id, nombre, descripcion, activo, fecharegistro, fechaactualizacion FROM categorias_servicio WHERE id=@id", ("id", id.ToString()));
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct) ? Leer(lector) : null;
    }
    public async Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT COUNT(*) FROM categorias_servicio WHERE UPPER(nombre)=UPPER(@identificador) AND id<>@id", ("identificador", identificador), ("id", exceptoId.ToString()));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) > 0;
    }
    public async Task<Resultado<CategoriaServicio>> GuardarAsync(CategoriaServicio entidad, bool nuevo, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        var sql = nuevo ? "INSERT INTO categorias_servicio (id, nombre, descripcion, activo, fecharegistro, fechaactualizacion) VALUES (@id, @nombre, @descripcion, @activo, @fecharegistro, @fechaactualizacion)" : "UPDATE categorias_servicio SET nombre=@nombre, descripcion=@descripcion, activo=@activo, fechaactualizacion=@fechaactualizacion WHERE id=@id";
        await using var comando = Sql.Comando(conexion, sql, ("id", entidad.Id.ToString()), ("nombre", entidad.Nombre), ("descripcion", entidad.Descripcion), ("activo", entidad.Activo), ("fecharegistro", entidad.FechaRegistro.UtcDateTime), ("fechaactualizacion", entidad.FechaActualizacion.UtcDateTime));
        try
        {
            var filas = await comando.ExecuteNonQueryAsync(ct);
            return filas == 1 ? Resultado<CategoriaServicio>.Exito(entidad) : Resultado<CategoriaServicio>.Fallo("categoriaservicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado);
        }
        catch (MySqlException ex) when (ex.Number == 1062) { return Resultado<CategoriaServicio>.Fallo("categoriaservicio.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto); }
        catch (MySqlException ex) when (ex.Number is 1451 or 1452) { return Resultado<CategoriaServicio>.Fallo("categoriaservicio.relacion", "La relación indicada ya no está disponible.", TipoError.Conflicto); }
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "DELETE FROM categorias_servicio WHERE id=@id", ("id", id.ToString()));
        try { return await comando.ExecuteNonQueryAsync(ct) == 1 ? Resultado<bool>.Exito(true) : Resultado<bool>.Fallo("categoriaservicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado); }
        catch (MySqlException ex) when (ex.Number is 1451 or 1452) { return Resultado<bool>.Fallo("categoriaservicio.en_uso", "No se puede eliminar: existen registros relacionados.", TipoError.Conflicto); }
    }
}
