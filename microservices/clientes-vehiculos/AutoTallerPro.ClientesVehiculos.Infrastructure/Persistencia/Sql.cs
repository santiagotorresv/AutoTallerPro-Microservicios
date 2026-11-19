using System.Data.Common;
namespace AutoTallerPro.ClientesVehiculos.Infrastructure.Persistencia;
internal static class Sql
{
    internal static DbCommand Comando(DbConnection conexion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        var comando = conexion.CreateCommand(); comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            var parametro = comando.CreateParameter(); parametro.ParameterName = nombre;
            parametro.Value = valor ?? DBNull.Value; comando.Parameters.Add(parametro);
        }
        return comando;
    }
    internal static string? Texto(DbDataReader lector, string nombre) => lector[nombre] is DBNull ? null : (string)lector[nombre];
    internal static DateTimeOffset Fecha(DbDataReader lector, string nombre)
        => new(DateTime.SpecifyKind((DateTime)lector[nombre], DateTimeKind.Utc));
}
