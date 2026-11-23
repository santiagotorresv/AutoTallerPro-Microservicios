using Npgsql;
namespace AutoTallerPro.ClientesVehiculos.Api.Persistencia;

public static class ConfiguracionPersistencia
{
    public static string Cadena(IConfiguration configuracion)
    {
        if (!string.IsNullOrWhiteSpace(configuracion["Database:ConnectionString"]))
        {
            NpgsqlConnectionStringBuilder opciones;
            try { opciones = new("SSL Mode=VerifyFull;" + configuracion["Database:ConnectionString"]); }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("Database:ConnectionString debe usar el formato Npgsql: Host=...;Database=...;Username=...;Password=...;SSL Mode=VerifyFull.");
            }
            if (opciones.SslMode != SslMode.VerifyFull)
                throw new InvalidOperationException("La conexión remota PostgreSQL requiere SSL Mode=VerifyFull.");
            if (string.IsNullOrWhiteSpace(opciones.Host) || string.IsNullOrWhiteSpace(opciones.Database) || string.IsNullOrWhiteSpace(opciones.Username))
                throw new InvalidOperationException("Faltan Host, Database o Username en la conexión PostgreSQL.");
            return opciones.ConnectionString;
        }
        string Requerido(string clave) => !string.IsNullOrWhiteSpace(configuracion[$"Database:{clave}"])
            ? configuracion[$"Database:{clave}"]! : throw new InvalidOperationException($"Falta Database:{clave}.");
        return new NpgsqlConnectionStringBuilder
        {
            Host = Requerido("Host"), Port = int.Parse(configuracion["Database:Port"] ?? "5432"),
            Username = Requerido("User"), Password = Requerido("Password"), Database = Requerido("Name")
        }.ConnectionString;
    }
}
