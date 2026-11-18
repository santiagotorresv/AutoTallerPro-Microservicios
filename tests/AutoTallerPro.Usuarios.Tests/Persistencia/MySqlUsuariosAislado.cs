using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MySqlUsuariosAislado.Variable)))
        {
            Skip = $"Define {MySqlUsuariosAislado.Variable} con una base MySQL de prueba vacía y aislada.";
        }
    }
}

public sealed class MySqlUsuariosAislado : IAsyncLifetime
{
    internal const string Variable = "AUTOTALLER_USUARIOS_TEST_MYSQL";

    private MySqlConnectionStringBuilder _opciones = null!;
    internal WebApplicationFactory<Program> Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        string cadena = Environment.GetEnvironmentVariable(Variable)
            ?? throw new InvalidOperationException($"Define {Variable} para ejecutar la integración MySQL.");
        _opciones = new MySqlConnectionStringBuilder(cadena);
        if (_opciones.Server != "127.0.0.1" || _opciones.UserID != "usuarios_pruebas"
            || !Regex.IsMatch(_opciones.Database, @"\Ausuarios_pruebas_[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Usa la instancia y la cuenta aisladas descritas en la guía de Usuarios.");
        }

        await using var conexion = new MySqlConnection(_opciones.ConnectionString);
        await conexion.OpenAsync();
        await using var comando = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE();", conexion);
        if (Convert.ToInt64(await comando.ExecuteScalarAsync()) != 0)
        {
            throw new InvalidOperationException("La base de prueba debe estar vacía antes de inicializarla.");
        }

        await AplicarEsquemaAsync();
        Api = CrearApi();
    }

    internal WebApplicationFactory<Program> CrearApi(string? password = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseSetting("Database:Host", _opciones.Server)
            .UseSetting("Database:Port", _opciones.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("Database:Name", _opciones.Database)
            .UseSetting("Database:User", _opciones.UserID)
            .UseSetting("Database:Password", password ?? _opciones.Password));

    internal async Task AplicarEsquemaAsync()
    {
        await using var conexion = new MySqlConnection(_opciones.ConnectionString);
        await conexion.OpenAsync();
        foreach (string archivo in new[] { "001_persistencia_usuarios.sql", "002_altas_usuarios.sql" })
        {
            string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", archivo));
            await using var comando = new MySqlCommand(sql, conexion);
            await comando.ExecuteNonQueryAsync();
        }
    }

    internal MySqlConnection CrearConexion() => new(_opciones.ConnectionString);

    public async Task DisposeAsync()
    {
        if (Api is not null)
        {
            await Api.DisposeAsync();
        }
    }
}
