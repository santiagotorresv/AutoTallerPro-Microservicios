using Npgsql;
using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.ClientesVehiculos.Infrastructure.Persistencia;
public sealed class VehiculoRepositorio(Conexion conexiones) : IVehiculoRepositorio
{
    private const string Columnas = "id, clienteid, placa, esplacaextranjera, marca, modelo, anio, color, activo, fecharegistro, fechaactualizacion, usuarioregistro, usuarioactualizacion";
    private static Vehiculo Leer(System.Data.Common.DbDataReader lector) => Vehiculo.Restaurar((Guid)lector["id"], (Guid)lector["clienteid"], (string)lector["placa"], Convert.ToBoolean(lector["esplacaextranjera"]), (string)lector["marca"], (string)lector["modelo"], Convert.ToInt32(lector["anio"]), Sql.Texto(lector, "color"), Convert.ToBoolean(lector["activo"]), Sql.Fecha(lector, "fecharegistro"), Sql.Fecha(lector, "fechaactualizacion"), Sql.Texto(lector, "usuarioregistro"), Sql.Texto(lector, "usuarioactualizacion"));
    public async Task<IReadOnlyList<Vehiculo>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT " + Columnas + " FROM vehiculos");
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var entidades = new List<Vehiculo>(); while (await lector.ReadAsync(ct)) entidades.Add(Leer(lector));
        return entidades;
    }
    public async Task<Vehiculo?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT " + Columnas + " FROM vehiculos WHERE id=@id", ("id", id));
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct) ? Leer(lector) : null;
    }
    public async Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT COUNT(*) FROM vehiculos WHERE UPPER(placa)=UPPER(@identificador) AND id<>@id", ("identificador", identificador), ("id", exceptoId));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) > 0;
    }
    public async Task<Resultado<Vehiculo>> GuardarAsync(Vehiculo entidad, bool nuevo, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        var sql = nuevo ? "INSERT INTO vehiculos (" + Columnas + ") VALUES (@id, @clienteid, @placa, @esplacaextranjera, @marca, @modelo, @anio, @color, @activo, @fecharegistro, @fechaactualizacion, @usuarioregistro, @usuarioactualizacion)" : "UPDATE vehiculos SET clienteid=@clienteid, placa=@placa, esplacaextranjera=@esplacaextranjera, marca=@marca, modelo=@modelo, anio=@anio, color=@color, activo=@activo, fechaactualizacion=@fechaactualizacion, usuarioactualizacion=@usuarioactualizacion WHERE id=@id";
        await using var comando = Sql.Comando(conexion, sql, ("id", entidad.Id), ("clienteid", entidad.ClienteId), ("placa", entidad.Placa), ("esplacaextranjera", entidad.EsPlacaExtranjera), ("marca", entidad.Marca), ("modelo", entidad.Modelo), ("anio", entidad.Anio), ("color", entidad.Color), ("activo", entidad.Activo), ("fecharegistro", entidad.FechaRegistro.UtcDateTime), ("fechaactualizacion", entidad.FechaActualizacion.UtcDateTime), ("usuarioregistro", entidad.UsuarioRegistro), ("usuarioactualizacion", entidad.UsuarioActualizacion));
        try
        {
            var filas = await comando.ExecuteNonQueryAsync(ct);
            return filas == 1 ? Resultado<Vehiculo>.Exito(entidad) : Resultado<Vehiculo>.Fallo("vehiculo.ausente", "No se encontró el registro.", TipoError.NoEncontrado);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") { return Resultado<Vehiculo>.Fallo("vehiculo.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto); }
        catch (PostgresException ex) when (ex.SqlState == "23503") { return Resultado<Vehiculo>.Fallo("vehiculo.relacion", "La relación indicada ya no está disponible.", TipoError.Conflicto); }
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "DELETE FROM vehiculos WHERE id=@id", ("id", id));
        try { return await comando.ExecuteNonQueryAsync(ct) == 1 ? Resultado<bool>.Exito(true) : Resultado<bool>.Fallo("vehiculo.ausente", "No se encontró el registro.", TipoError.NoEncontrado); }
        catch (PostgresException ex) when (ex.SqlState == "23503") { return Resultado<bool>.Fallo("vehiculo.en_uso", "No se puede eliminar: existen registros relacionados.", TipoError.Conflicto); }
    }
}
