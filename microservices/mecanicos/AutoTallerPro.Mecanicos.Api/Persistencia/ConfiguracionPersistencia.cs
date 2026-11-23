using MongoDB.Driver;
namespace AutoTallerPro.Mecanicos.Api.Persistencia;

public static class ConfiguracionPersistencia
{
    public static MongoClientSettings Opciones(IConfiguration configuracion)
    {
        if (!string.IsNullOrWhiteSpace(configuracion["Database:ConnectionString"]))
        {
            MongoClientSettings opciones;
            try { opciones = MongoClientSettings.FromConnectionString(configuracion["Database:ConnectionString"]); }
            catch (Exception ex) when (ex is ArgumentException or FormatException or MongoConfigurationException)
            {
                throw new InvalidOperationException("Database:ConnectionString no es una conexión MongoDB válida.");
            }
            if (!opciones.UseTls || opciones.AllowInsecureTls || opciones.SslSettings?.ServerCertificateValidationCallback is not null)
                throw new InvalidOperationException("La conexión remota MongoDB requiere TLS con validación de certificados.");
            opciones.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            return opciones;
        }
        string Requerido(string clave) => !string.IsNullOrWhiteSpace(configuracion[$"Database:{clave}"])
            ? configuracion[$"Database:{clave}"]! : throw new InvalidOperationException($"Falta Database:{clave}.");
        return new MongoClientSettings
        {
            Server = new MongoServerAddress(Requerido("Host"), int.Parse(configuracion["Database:Port"] ?? "27017")),
            Credential = MongoCredential.CreateCredential(configuracion["Database:AuthSource"] ?? Requerido("Name"), Requerido("User"), Requerido("Password")),
            ServerSelectionTimeout = TimeSpan.FromSeconds(10)
        };
    }
}
