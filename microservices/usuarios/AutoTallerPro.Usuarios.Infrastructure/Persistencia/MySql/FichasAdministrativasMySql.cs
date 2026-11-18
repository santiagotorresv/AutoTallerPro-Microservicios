using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Infrastructure.Persistencia.MySql;

public sealed class FichasAdministrativasMySql(MySqlDataSource origen) : IFichasAdministrativas, IConsultaFichasUsuarios
{
    private const string Seleccion = """
        SELECT Id, KeycloakEmisor, KeycloakSujeto, FechaCreacionUtc, CreadoPorEmisor, CreadoPorSujeto,
               FechaModificacionUtc, ModificadoPorEmisor, ModificadoPorSujeto
        FROM FichasAdministrativas
        """;

    public async Task<int> CrearAsync(
        IdentidadKeycloak identidad, DateTimeOffset fechaCreacion,
        IdentidadKeycloak? actor, CancellationToken cancellationToken = default)
    {
        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        return await CrearAsync(conexion, null, identidad, fechaCreacion, actor, cancellationToken);
    }

    internal static async Task<int> CrearAsync(
        MySqlConnection conexion, MySqlTransaction? transaccion, IdentidadKeycloak identidad,
        DateTimeOffset fechaCreacion, IdentidadKeycloak? actor, CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = """
            INSERT INTO FichasAdministrativas
                (KeycloakEmisor, KeycloakSujeto, FechaCreacionUtc, CreadoPorEmisor, CreadoPorSujeto)
            VALUES (@Emisor, @Sujeto, @Fecha, @ActorEmisor, @ActorSujeto);
            """;
        comando.Parameters.AddWithValue("@Emisor", identidad.Emisor);
        comando.Parameters.AddWithValue("@Sujeto", identidad.Sujeto);
        comando.Parameters.AddWithValue("@Fecha", fechaCreacion.UtcDateTime);
        comando.Parameters.AddWithValue("@ActorEmisor", actor?.Emisor);
        comando.Parameters.AddWithValue("@ActorSujeto", actor?.Sujeto);

        try
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
            return checked((int)comando.LastInsertedId);
        }
        catch (MySqlException error) when (error.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            throw new IdentidadYaVinculadaException(error);
        }
    }

    public Task<FichaAdministrativa?> BuscarPorIdAsync(int id, CancellationToken cancellationToken = default) =>
        BuscarAsync(" WHERE Id = @Id;", [new MySqlParameter("@Id", id)], cancellationToken);

    public Task<FichaAdministrativa?> BuscarPorIdentidadAsync(
        IdentidadKeycloak identidad, CancellationToken cancellationToken = default) =>
        BuscarAsync(" WHERE KeycloakEmisor = @Emisor AND KeycloakSujeto = @Sujeto;",
            [new MySqlParameter("@Emisor", identidad.Emisor), new MySqlParameter("@Sujeto", identidad.Sujeto)],
            cancellationToken);

    public Task<IReadOnlyList<FichaUsuario>> ListarAsync(string emisor, CancellationToken cancellationToken) =>
        ConsultarAsync(emisor, null, cancellationToken);

    public async Task<FichaUsuario?> ObtenerAsync(string emisor, int id, CancellationToken cancellationToken) =>
        (await ConsultarAsync(emisor, id, cancellationToken)).SingleOrDefault();

    private async Task<IReadOnlyList<FichaUsuario>> ConsultarAsync(
        string emisor, int? id, CancellationToken cancellationToken)
    {
        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            SELECT f.Id, f.KeycloakSujeto, a.Estado
            FROM FichasAdministrativas f LEFT JOIN AltasUsuarios a ON a.FichaId = f.Id
            WHERE f.KeycloakEmisor = @Emisor
            """;
        comando.Parameters.AddWithValue("@Emisor", emisor);
        if (id is not null)
        {
            comando.CommandText += " AND f.Id = @Id";
            comando.Parameters.AddWithValue("@Id", id);
        }

        var fichas = new List<FichaUsuario>();
        await using var filas = await comando.ExecuteReaderAsync(cancellationToken);
        while (await filas.ReadAsync(cancellationToken))
        {
            fichas.Add(new FichaUsuario(filas.GetInt32(0), filas.GetString(1),
                filas.IsDBNull(2) ? null : (EstadoAlta)filas.GetByte(2)));
        }

        return fichas;
    }

    public async Task<bool> ActualizarMetadatosAsync(
        int id, DateTimeOffset fechaModificacion,
        IdentidadKeycloak? actor, CancellationToken cancellationToken = default)
    {
        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            UPDATE FichasAdministrativas
            SET FechaModificacionUtc = @Fecha, ModificadoPorEmisor = @ActorEmisor,
                ModificadoPorSujeto = @ActorSujeto
            WHERE Id = @Id;
            """;
        comando.Parameters.AddWithValue("@Id", id);
        comando.Parameters.AddWithValue("@Fecha", fechaModificacion.UtcDateTime);
        comando.Parameters.AddWithValue("@ActorEmisor", actor?.Emisor);
        comando.Parameters.AddWithValue("@ActorSujeto", actor?.Sujeto);

        return await comando.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task<FichaAdministrativa?> BuscarAsync(
        string filtro, MySqlParameter[] parametros, CancellationToken cancellationToken)
    {
        await using var conexion = await origen.OpenConnectionAsync(cancellationToken);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = Seleccion + filtro;
        comando.Parameters.AddRange(parametros);

        await using var filas = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await filas.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new FichaAdministrativa(
            filas.GetInt32(0),
            new IdentidadKeycloak(filas.GetString(1), filas.GetString(2)),
            new DateTimeOffset(filas.GetDateTime(3), TimeSpan.Zero),
            LeerActor(filas, 4, 5),
            filas.IsDBNull(6) ? null : new DateTimeOffset(filas.GetDateTime(6), TimeSpan.Zero),
            LeerActor(filas, 7, 8));
    }

    private static IdentidadKeycloak? LeerActor(MySqlDataReader filas, int emisor, int sujeto) =>
        filas.IsDBNull(emisor) ? null : new IdentidadKeycloak(filas.GetString(emisor), filas.GetString(sujeto));
}
