using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Infrastructure.Persistencia.MySql;

public sealed class AuditoriaAdministrativaMySql(MySqlDataSource origen) : IAuditoriaAdministrativa
{
    public async Task<long> RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        return await RegistrarAsync(conexion, null, registro, cancellationToken);
    }

    internal static async Task<long> RegistrarAsync(
        MySqlConnection conexion, MySqlTransaction? transaccion, RegistroAuditoria registro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registro);
        if (registro.CodigoMotivo is { } codigo
            && (codigo.Length is < 1 or > 64 || !char.IsAsciiLetterUpper(codigo[0])
                || !codigo.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')))
        {
            throw new ArgumentException("El motivo debe ser un código sin texto libre.", nameof(registro));
        }

        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = """
            INSERT INTO AuditoriaAdministrativa
                (ActorEmisor, ActorSujeto, Accion, Entidad, ObjetivoId, FechaUtc, Resultado, CodigoMotivo, CorrelacionId)
            VALUES (@Emisor, @Sujeto, @Accion, @Entidad, @ObjetivoId, @Fecha, @Resultado, @Motivo, @Correlacion);
            """;
        comando.Parameters.AddWithValue("@Emisor", registro.Actor.Emisor);
        comando.Parameters.AddWithValue("@Sujeto", registro.Actor.Sujeto);
        comando.Parameters.AddWithValue("@Accion", registro.Accion);
        comando.Parameters.AddWithValue("@Entidad", registro.Entidad);
        comando.Parameters.AddWithValue("@ObjetivoId", registro.ObjetivoId);
        comando.Parameters.AddWithValue("@Fecha", registro.FechaUtc.UtcDateTime);
        comando.Parameters.AddWithValue("@Resultado", registro.Resultado.ToString());
        comando.Parameters.AddWithValue("@Motivo", registro.CodigoMotivo);
        comando.Parameters.AddWithValue("@Correlacion", registro.CorrelacionId);
        await comando.ExecuteNonQueryAsync(cancellationToken);

        return comando.LastInsertedId;
    }

    public async Task<IReadOnlyList<RegistroAuditoria>> ObtenerRecientesAsync(
        int cantidad, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cantidad, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cantidad, 500);

        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT Id, ActorEmisor, ActorSujeto, Accion, Entidad, ObjetivoId, FechaUtc, Resultado,
                   CodigoMotivo, CorrelacionId
            FROM AuditoriaAdministrativa
            ORDER BY FechaUtc DESC, Id DESC
            LIMIT @Cantidad;
            """;
        comando.Parameters.AddWithValue("@Cantidad", cantidad);

        var registros = new List<RegistroAuditoria>();
        await using var filas = await comando.ExecuteReaderAsync(cancellationToken);
        while (await filas.ReadAsync(cancellationToken))
        {
            registros.Add(new RegistroAuditoria(
                new IdentidadKeycloak(filas.GetString(1), filas.GetString(2)),
                filas.GetString(3),
                filas.GetString(4),
                filas.GetString(5),
                new DateTimeOffset(filas.GetDateTime(6), TimeSpan.Zero),
                Enum.Parse<ResultadoAuditoria>(filas.GetString(7)),
                filas.IsDBNull(8) ? null : filas.GetString(8),
                filas.IsDBNull(9) ? null : filas.GetString(9))
            {
                Id = filas.GetInt64(0)
            });
        }

        return registros;
    }
}
