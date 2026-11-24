using Npgsql;
using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.ClientesVehiculos.Infrastructure.Persistencia;
public sealed class ClienteRepositorio(Conexion conexiones) : IClienteRepositorio
{
    private const string Columnas = "id, documento, nombre, telefono, correo, activo, fecharegistro, fechaactualizacion, usuarioregistro, usuarioactualizacion";
    private static Cliente Leer(System.Data.Common.DbDataReader lector) => Cliente.Restaurar((Guid)lector["id"], (string)lector["documento"], (string)lector["nombre"], Sql.Texto(lector, "telefono"), Sql.Texto(lector, "correo"), Convert.ToBoolean(lector["activo"]), Sql.Fecha(lector, "fecharegistro"), Sql.Fecha(lector, "fechaactualizacion"), Sql.Texto(lector, "usuarioregistro"), Sql.Texto(lector, "usuarioactualizacion"));
    public async Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT " + Columnas + " FROM clientes");
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var entidades = new List<Cliente>(); while (await lector.ReadAsync(ct)) entidades.Add(Leer(lector));
        return entidades;
    }
    public async Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT " + Columnas + " FROM clientes WHERE id=@id", ("id", id));
        await using var lector = await comando.ExecuteReaderAsync(ct);
        return await lector.ReadAsync(ct) ? Leer(lector) : null;
    }
    public async Task<bool> ExisteIdentificadorAsync(string identificador, Guid exceptoId, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "SELECT COUNT(*) FROM clientes WHERE UPPER(documento)=UPPER(@identificador) AND id<>@id", ("identificador", identificador), ("id", exceptoId));
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct)) > 0;
    }
    public async Task<Resultado<Cliente>> GuardarAsync(Cliente entidad, bool nuevo, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        var sql = nuevo ? "INSERT INTO clientes (" + Columnas + ") VALUES (@id, @documento, @nombre, @telefono, @correo, @activo, @fecharegistro, @fechaactualizacion, @usuarioregistro, @usuarioactualizacion)" : "UPDATE clientes SET documento=@documento, nombre=@nombre, telefono=@telefono, correo=@correo, activo=@activo, fechaactualizacion=@fechaactualizacion, usuarioactualizacion=@usuarioactualizacion WHERE id=@id";
        await using var comando = Sql.Comando(conexion, sql, ("id", entidad.Id), ("documento", entidad.Documento), ("nombre", entidad.Nombre), ("telefono", entidad.Telefono), ("correo", entidad.Correo), ("activo", entidad.Activo), ("fecharegistro", entidad.FechaRegistro.UtcDateTime), ("fechaactualizacion", entidad.FechaActualizacion.UtcDateTime), ("usuarioregistro", entidad.UsuarioRegistro), ("usuarioactualizacion", entidad.UsuarioActualizacion));
        try
        {
            var filas = await comando.ExecuteNonQueryAsync(ct);
            return filas == 1 ? Resultado<Cliente>.Exito(entidad) : Resultado<Cliente>.Fallo("cliente.ausente", "No se encontró el registro.", TipoError.NoEncontrado);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505") { return Resultado<Cliente>.Fallo("cliente.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto); }
        catch (PostgresException ex) when (ex.SqlState == "23503") { return Resultado<Cliente>.Fallo("cliente.relacion", "La relación indicada ya no está disponible.", TipoError.Conflicto); }
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        await using var conexion = conexiones.Crear(); await conexion.OpenAsync(ct);
        await using var comando = Sql.Comando(conexion, "DELETE FROM clientes WHERE id=@id", ("id", id));
        try { return await comando.ExecuteNonQueryAsync(ct) == 1 ? Resultado<bool>.Exito(true) : Resultado<bool>.Fallo("cliente.ausente", "No se encontró el registro.", TipoError.NoEncontrado); }
        catch (PostgresException ex) when (ex.SqlState == "23503") { return Resultado<bool>.Fallo("cliente.en_uso", "No se puede eliminar: existen registros relacionados.", TipoError.Conflicto); }
    }
}
