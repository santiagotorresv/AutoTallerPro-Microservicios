using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Infrastructure.Persistencia.MySql;

public sealed class AltasUsuariosMySql(MySqlDataSource origen) : IAltasUsuarios
{
    public async Task<IAltaUsuario> AbrirAsync(
        IdentidadKeycloak actor, Guid clave, string correo, byte[] huella, CancellationToken cancellationToken)
    {
        var conexion = await origen.OpenConnectionAsync(cancellationToken);
        string bloqueo = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{conexion.Database}:p44:{actor.Emisor}:{clave:D}")));
        bool adquirido = false;
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT GET_LOCK(@Bloqueo, 0);";
            comando.Parameters.AddWithValue("@Bloqueo", bloqueo);
            adquirido = Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken)) == 1;
            if (!adquirido)
            {
                throw new OperacionUsuariosException(ErrorUsuarios.AltaEnCurso);
            }

            AltaUsuario alta = await LeerOReservarAsync(conexion, actor, clave, correo, huella, cancellationToken);
            return new Sesion(conexion, bloqueo, actor, alta);
        }
        catch
        {
            await CerrarAsync(conexion, adquirido ? bloqueo : null);
            throw;
        }
    }

    private static async Task<AltaUsuario> LeerOReservarAsync(
        MySqlConnection conexion, IdentidadKeycloak actor, Guid clave, string correo, byte[] huella,
        CancellationToken cancellationToken)
    {
        await using var comando = conexion.CreateCommand();
        comando.Parameters.AddWithValue("@Emisor", actor.Emisor);
        comando.Parameters.AddWithValue("@Clave", clave.ToString("D"));
        comando.CommandText = """
            SELECT Id, ActorSujeto, Huella, Username, Sujeto, FichaId, Estado
            FROM AltasUsuarios WHERE Emisor = @Emisor AND Clave = @Clave;
            """;
        await using (var fila = await comando.ExecuteReaderAsync(cancellationToken))
        {
            if (await fila.ReadAsync(cancellationToken))
            {
                if (fila.GetString(1) != actor.Sujeto || !CryptographicOperations.FixedTimeEquals((byte[])fila[2], huella))
                {
                    throw new OperacionUsuariosException(ErrorUsuarios.ClaveReutilizada);
                }

                return new AltaUsuario(fila.GetGuid(0), fila.IsDBNull(3) ? null : fila.GetString(3),
                    fila.IsDBNull(4) ? null : fila.GetString(4), fila.IsDBNull(5) ? null : fila.GetInt32(5),
                    (EstadoAlta)fila.GetByte(6));
            }
        }

        var id = Guid.NewGuid();
        comando.CommandText = """
            INSERT INTO AltasUsuarios (Id, Emisor, Clave, ActorSujeto, Huella, CorreoReservado)
            VALUES (@Id, @Emisor, @Clave, @Actor, @Huella, @Correo);
            """;
        comando.Parameters.AddWithValue("@Id", id.ToString("D"));
        comando.Parameters.AddWithValue("@Actor", actor.Sujeto);
        comando.Parameters.AddWithValue("@Huella", huella);
        comando.Parameters.AddWithValue("@Correo", correo);
        try
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException error) when (error.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            throw new OperacionUsuariosException(ErrorUsuarios.CorreoDuplicado);
        }

        return new AltaUsuario(id, null, null, null, EstadoAlta.Preparada);
    }

    private static async ValueTask CerrarAsync(MySqlConnection conexion, string? bloqueo)
    {
        await using (conexion)
        {
            if (bloqueo is not null && conexion.State == System.Data.ConnectionState.Open)
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT RELEASE_LOCK(@Bloqueo);";
                comando.CommandTimeout = 5;
                comando.Parameters.AddWithValue("@Bloqueo", bloqueo);
                try
                {
                    await comando.ExecuteScalarAsync();
                }
                catch (MySqlException)
                {
                }
            }
        }
    }

    private sealed class Sesion(MySqlConnection conexion, string bloqueo, IdentidadKeycloak actor, AltaUsuario alta)
        : IAltaUsuario
    {
        public AltaUsuario Alta { get; private set; } = alta;

        public async Task GuardarCandidatoAsync(string username, CancellationToken cancellationToken)
        {
            await ActualizarAsync("Username = @Valor", username, cancellationToken);
            Alta = Alta with { Username = username };
        }

        public async Task GuardarSujetoAsync(string sujeto, CancellationToken cancellationToken)
        {
            await ActualizarAsync("Sujeto = @Valor", sujeto, cancellationToken);
            Alta = Alta with { Sujeto = sujeto };
        }

        public async Task GuardarEstadoAsync(EstadoAlta estado, CancellationToken cancellationToken)
        {
            string cambios = estado is EstadoAlta.Conflicto or EstadoAlta.PerfilRechazado
                ? "Estado = @Valor, CorreoReservado = NULL"
                : "Estado = @Valor";
            try
            {
                await ActualizarAsync(cambios, (byte)estado, cancellationToken);
            }
            catch (MySqlException) when (estado is EstadoAlta.InvitacionPendiente
                or EstadoAlta.EnvioIncierto or EstadoAlta.CorreoSolicitado)
            {
                throw new PersistenciaEstadoAltaException();
            }
            Alta = Alta with { Estado = estado };
        }

        public async Task VincularFichaAsync(DateTimeOffset fecha, CancellationToken cancellationToken)
        {
            var identidad = new IdentidadKeycloak(actor.Emisor, Alta.Sujeto!);
            await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
            int id = await FichasAdministrativasMySql.CrearAsync(
                conexion, transaccion, identidad, fecha, actor, cancellationToken);
            await AuditoriaAdministrativaMySql.RegistrarAsync(conexion, transaccion,
                new RegistroAuditoria(actor, "USUARIO_CREADO", "Usuario", id.ToString(CultureInfo.InvariantCulture),
                    fecha, ResultadoAuditoria.Exitoso, CorrelacionId: Alta.Id.ToString("D")), cancellationToken);

            await using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText = """
                UPDATE AltasUsuarios SET FichaId = @Ficha, Estado = @Estado, CorreoReservado = NULL
                WHERE Id = @Id;
                """;
            comando.Parameters.AddWithValue("@Ficha", id);
            comando.Parameters.AddWithValue("@Estado", (byte)EstadoAlta.InvitacionPendiente);
            comando.Parameters.AddWithValue("@Id", Alta.Id.ToString("D"));
            await comando.ExecuteNonQueryAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            Alta = Alta with { FichaId = id, Estado = EstadoAlta.InvitacionPendiente };
        }

        private async Task ActualizarAsync(string cambios, object valor, CancellationToken cancellationToken)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"UPDATE AltasUsuarios SET {cambios} WHERE Id = @Id;";
            comando.Parameters.AddWithValue("@Id", Alta.Id.ToString("D"));
            comando.Parameters.AddWithValue("@Valor", valor);
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => CerrarAsync(conexion, bloqueo);
    }
}

