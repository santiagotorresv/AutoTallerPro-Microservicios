using MySqlConnector;
using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
using AutoTallerPro.Servicios.Application.Puertos;
namespace AutoTallerPro.Servicios.Infrastructure.Persistencia;
public sealed class ServicioRepositorio(Conexion conexiones) : IServicioRepositorio
{
    private static Servicio Leer(System.Data.Common.DbDataReader lector) => Servicio.Restaurar(Guid.Parse(Convert.ToString(lector["id"])!), Guid.Parse(Convert.ToString(lector["categoriaservicioid"])!), (string)lector["nombre"], Sql.Texto(lector, "descripcion"), (decimal)lector["preciobase"], lector["duracionestimada"] is DBNull ? null : Convert.ToInt32(lector["duracionestimada"]), Convert.ToBoolean(lector["activo"]), Sql.Fecha(lector, "fecharegistro"), Sql.Fecha(lector, "fechaactualizacion"));
    public async Task<IReadOnlyList<Servicio>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT id, categoriaservicioid, nombre, descripcion, preciobase, duracionestimada, activo, fecharegistro, fechaactualizacion FROM servicios");
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var entidades = new List<Servicio>(); while (await lector.ReadAsync(ct)) entidades.Add(Leer(lector));
        return entidades;
    }
    public async Task<Servicio?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT id, categoriaservicioid, nombre, descripcion, preciobase, duracionestimada, activo, fecharegistro, fechaactualizacion FROM servicios WHERE id=@id", ("id", id.ToString()));
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct) ? Leer(lector) : null;
    }
    public async Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT COUNT(*) FROM servicios WHERE UPPER(nombre)=UPPER(@identificador) AND id<>@id", ("identificador", identificador), ("id", exceptoId.ToString()));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) > 0;
    }
    public async Task<Resultado<Servicio>> GuardarAsync(Servicio entidad, bool nuevo, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        var sql = nuevo ? "INSERT INTO servicios (id, categoriaservicioid, nombre, descripcion, preciobase, duracionestimada, activo, fecharegistro, fechaactualizacion) VALUES (@id, @categoriaservicioid, @nombre, @descripcion, @preciobase, @duracionestimada, @activo, @fecharegistro, @fechaactualizacion)" : "UPDATE servicios SET categoriaservicioid=@categoriaservicioid, nombre=@nombre, descripcion=@descripcion, preciobase=@preciobase, duracionestimada=@duracionestimada, activo=@activo, fechaactualizacion=@fechaactualizacion WHERE id=@id";
        await using var comando = Sql.Comando(conexion, sql, ("id", entidad.Id.ToString()), ("categoriaservicioid", entidad.CategoriaServicioId.ToString()), ("nombre", entidad.Nombre), ("descripcion", entidad.Descripcion), ("preciobase", entidad.PrecioBase), ("duracionestimada", entidad.DuracionEstimada), ("activo", entidad.Activo), ("fecharegistro", entidad.FechaRegistro.UtcDateTime), ("fechaactualizacion", entidad.FechaActualizacion.UtcDateTime));
        try
        {
            var filas = await comando.ExecuteNonQueryAsync(ct);
            return filas == 1 ? Resultado<Servicio>.Exito(entidad) : Resultado<Servicio>.Fallo("servicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado);
        }
        catch (MySqlException ex) when (ex.Number == 1062) { return Resultado<Servicio>.Fallo("servicio.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto); }
        catch (MySqlException ex) when (ex.Number is 1451 or 1452) { return Resultado<Servicio>.Fallo("servicio.relacion", "La relación indicada ya no está disponible.", TipoError.Conflicto); }
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "DELETE FROM servicios WHERE id=@id", ("id", id.ToString()));
        try { return await comando.ExecuteNonQueryAsync(ct) == 1 ? Resultado<bool>.Exito(true) : Resultado<bool>.Fallo("servicio.ausente", "No se encontró el registro.", TipoError.NoEncontrado); }
        catch (MySqlException ex) when (ex.Number is 1451 or 1452) { return Resultado<bool>.Fallo("servicio.en_uso", "No se puede eliminar: existen registros relacionados.", TipoError.Conflicto); }
    }
}
