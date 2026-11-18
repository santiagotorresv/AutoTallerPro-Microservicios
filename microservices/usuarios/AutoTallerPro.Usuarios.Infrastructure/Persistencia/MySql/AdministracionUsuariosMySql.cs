using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Infrastructure.Persistencia.MySql;

public sealed class AdministracionUsuariosMySql(MySqlDataSource origen) : IAdministracionUsuarios
{
    public async Task<Resultado<ISesionAdministracionUsuarios>> AbrirAsync(string emisor, CancellationToken cancellationToken)
    {
        var conexion = await origen.OpenConnectionAsync(cancellationToken);
        string bloqueo = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{conexion.Database}:p45:{emisor}")));
        bool entregada = false;
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT GET_LOCK(@Bloqueo, 4);";
            comando.Parameters.AddWithValue("@Bloqueo", bloqueo);
            if (Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return Resultado<ISesionAdministracionUsuarios>.Fallido(ErrorUsuarios.AdministracionEnCurso);
            }

            var resultado = Resultado<ISesionAdministracionUsuarios>.Exitoso(new Sesion(conexion, emisor, bloqueo));
            entregada = true;
            return resultado;
        }
        finally
        {
            if (!entregada)
            {
                await conexion.DisposeAsync();
            }
        }
    }

    private sealed class Sesion(MySqlConnection conexion, string emisor, string bloqueo) : ISesionAdministracionUsuarios
    {
        public async Task<OperacionAdministrativa?> BuscarAsync(Guid clave, CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                SELECT Id, ActorSujeto, FichaId, Tipo, Huella, Cambios, Restantes, Paso, Estado, Error
                FROM OperacionesUsuarios WHERE Emisor = @Emisor AND Clave = @Clave;
                """, null, ("Emisor", emisor), ("Clave", clave.ToString("D")));
            await using var resultados = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await resultados.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new OperacionAdministrativa(resultados.GetGuid(0), clave,
                new IdentidadKeycloak(emisor, resultados.GetString(1)), resultados.GetInt32(2),
                (TipoAdministracion)resultados.GetByte(3), (byte[])resultados[4],
                (CambiosUsuario)resultados.GetByte(5), (CambiosUsuario)resultados.GetByte(6),
                (CambiosUsuario)resultados.GetByte(7), (EstadoOperacion)resultados.GetByte(8),
                resultados.IsDBNull(9) ? null : Enum.Parse<ErrorUsuarios>(resultados.GetString(9)));
        }

        public async Task<bool> HayEdicionPendienteAsync(Guid excepto, CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                SELECT EXISTS (SELECT 1 FROM OperacionesUsuarios
                    WHERE Emisor = @Emisor AND Clave <> @Clave AND Estado = 0 AND Tipo <> @Reenvio);
                """, null, ("Emisor", emisor), ("Clave", excepto.ToString("D")), ("Reenvio", (byte)TipoAdministracion.Reenviar));
            return Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken)) == 1;
        }

        public async Task AbrirOperacionAsync(OperacionAdministrativa operacion, DateTimeOffset fecha,
            CancellationToken cancellationToken)
        {
            await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
            await using var comando = Preparar("""
                INSERT INTO OperacionesUsuarios (Id, Emisor, Clave, ActorSujeto, FichaId, Tipo, Huella,
                    Cambios, Restantes, FechaUtc)
                VALUES (@Id, @Emisor, @Clave, @Actor, @Ficha, @Tipo, @Huella, @Cambios, @Cambios, @Fecha);
                """, transaccion, ("Id", operacion.Id.ToString("D")), ("Emisor", emisor), ("Clave", operacion.Clave.ToString("D")),
                ("Actor", operacion.Actor.Sujeto), ("Ficha", operacion.FichaId), ("Tipo", (byte)operacion.Tipo),
                ("Huella", operacion.Huella), ("Cambios", (byte)operacion.Cambios), ("Fecha", fecha.UtcDateTime));
            await comando.ExecuteNonQueryAsync(cancellationToken);
            await AuditarAsync(operacion, fecha, ResultadoAuditoria.Pendiente, transaccion, cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }

        public async Task GuardarPasoAsync(OperacionAdministrativa operacion, bool correoCambiado,
            CancellationToken cancellationToken)
        {
            await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
            await ActualizarOperacionAsync(operacion, transaccion, cancellationToken);
            if (correoCambiado)
            {
                await using var comando = Preparar("""
                    INSERT INTO ControlInvitacionesUsuarios (FichaId, ReservadoPor, UltimoIntentoUtc, Estado)
                    SELECT f.Id, @Id, IF(a.Estado IN (2, 3), f.FechaCreacionUtc, NULL), @Estado
                    FROM FichasAdministrativas f LEFT JOIN AltasUsuarios a ON a.FichaId = f.Id WHERE f.Id = @Ficha
                    ON DUPLICATE KEY UPDATE ReservadoPor = @Id, Estado = @Estado;
                    """, transaccion, ("Ficha", operacion.FichaId), ("Id", operacion.Id.ToString("D")),
                    ("Estado", (byte)EstadoAlta.InvitacionPendiente));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaccion.CommitAsync(cancellationToken);
        }

        public async Task CerrarOperacionAsync(OperacionAdministrativa operacion, DateTimeOffset fecha, bool modificarFicha,
            EstadoAlta? estadoInvitacion, CancellationToken cancellationToken)
        {
            await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
            await ActualizarOperacionAsync(operacion, transaccion, cancellationToken);
            if (modificarFicha)
            {
                await FichasAdministrativasMySql.ActualizarMetadatosAsync(conexion, transaccion, operacion.FichaId,
                    fecha, operacion.Actor, cancellationToken);
            }

            if (estadoInvitacion is { } invitacion)
            {
                await GuardarInvitacionAsync(operacion.FichaId, operacion.Id, invitacion, transaccion, cancellationToken);
            }

            var resultado = operacion.Estado switch
            {
                EstadoOperacion.Exitosa => ResultadoAuditoria.Exitoso,
                EstadoOperacion.Fallida => ResultadoAuditoria.Fallido,
                _ => ResultadoAuditoria.Pendiente
            };
            await AuditarAsync(operacion, fecha, resultado, transaccion, cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }

        public async Task<ControlInvitacion?> ObtenerInvitacionAsync(int fichaId, CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                SELECT COALESCE(c.ReservadoPor, a.Id),
                    CASE WHEN c.FichaId IS NOT NULL THEN c.UltimoIntentoUtc WHEN a.Estado IN (2,3) THEN f.FechaCreacionUtc END,
                    COALESCE(c.Estado, a.Estado), UTC_TIMESTAMP(6)
                FROM FichasAdministrativas f
                LEFT JOIN AltasUsuarios a ON a.FichaId = f.Id
                LEFT JOIN ControlInvitacionesUsuarios c ON c.FichaId = f.Id
                WHERE f.Id = @Ficha AND (c.FichaId IS NOT NULL OR a.FichaId IS NOT NULL);
                """, null, ("Ficha", fichaId));
            await using var datos = await comando.ExecuteReaderAsync(cancellationToken);
            return await datos.ReadAsync(cancellationToken)
                ? new ControlInvitacion(datos.GetGuid(0),
                    datos.IsDBNull(1) ? null : new DateTimeOffset(datos.GetDateTime(1), TimeSpan.Zero),
                    (EstadoAlta)datos.GetByte(2), new DateTimeOffset(datos.GetDateTime(3), TimeSpan.Zero))
                : null;
        }

        public async Task ReservarInvitacionAsync(int fichaId, Guid operacion, CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                INSERT INTO ControlInvitacionesUsuarios (FichaId, ReservadoPor, UltimoIntentoUtc, Estado)
                VALUES (@Ficha, @Id, UTC_TIMESTAMP(6), @Estado)
                ON DUPLICATE KEY UPDATE ReservadoPor = @Id, UltimoIntentoUtc = UTC_TIMESTAMP(6), Estado = @Estado;
                """, null, ("Ficha", fichaId), ("Id", operacion.ToString("D")),
                ("Estado", (byte)EstadoAlta.EnvioIncierto));
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task GuardarInvitacionAsync(int fichaId, Guid operacion, EstadoAlta estado, CancellationToken cancellationToken) =>
            GuardarInvitacionAsync(fichaId, operacion, estado, null, cancellationToken);

        private async Task GuardarInvitacionAsync(int fichaId, Guid operacion, EstadoAlta estado,
            MySqlTransaction? transaccion, CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                UPDATE ControlInvitacionesUsuarios SET Estado = @Estado WHERE FichaId = @Ficha AND ReservadoPor = @Id;
                """, transaccion, ("Ficha", fichaId), ("Id", operacion.ToString("D")), ("Estado", (byte)estado));
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task ActualizarOperacionAsync(OperacionAdministrativa operacion, MySqlTransaction transaccion,
            CancellationToken cancellationToken)
        {
            await using var comando = Preparar("""
                UPDATE OperacionesUsuarios SET Restantes = @Restantes, Paso = @Paso, Estado = @Estado, Error = @Error
                WHERE Id = @Id;
                """, transaccion, ("Restantes", (byte)operacion.Restantes), ("Paso", (byte)operacion.Paso),
                ("Estado", (byte)operacion.Estado), ("Error", operacion.Error?.ToString()), ("Id", operacion.Id.ToString("D")));
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        private Task<long> AuditarAsync(OperacionAdministrativa operacion, DateTimeOffset fecha, ResultadoAuditoria resultado,
            MySqlTransaction transaccion, CancellationToken cancellationToken) =>
            AuditoriaAdministrativaMySql.RegistrarAsync(conexion, transaccion,
                new RegistroAuditoria(operacion.Actor, AccionesAdministrativas.Nombre(operacion.Tipo), "Usuario",
                    operacion.FichaId.ToString(CultureInfo.InvariantCulture), fecha, resultado,
                    operacion.Error is { } error ? AccionesAdministrativas.Codigo(error) : $"CAMBIOS_{(int)operacion.Cambios}",
                    operacion.Id.ToString("D")), cancellationToken);

        private MySqlCommand Preparar(string sql, MySqlTransaction? transaccion, params (string Nombre, object? Valor)[] datos)
        {
            var comando = conexion.CreateCommand();
            comando.CommandText = sql;
            comando.Transaction = transaccion;
            foreach (var (nombre, valor) in datos)
            {
                comando.Parameters.AddWithValue("@" + nombre, valor);
            }
            return comando;
        }

        public async ValueTask DisposeAsync()
        {
            await using (conexion)
            {
                if (conexion.State == System.Data.ConnectionState.Open)
                {
                    await using var comando = Preparar("SELECT RELEASE_LOCK(@Bloqueo);", null, ("Bloqueo", bloqueo));
                    comando.CommandTimeout = 5;
                    try { await comando.ExecuteScalarAsync(); }
                    catch (MySqlException) { }
                }
            }
        }
    }
}
