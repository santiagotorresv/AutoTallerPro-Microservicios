using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Infrastructure.Persistencia.MySql;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Api.Persistencia;

internal static class ConfiguracionPersistenciaUsuarios
{
    internal static void AddPersistenciaUsuarios(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(_ =>
        {
            var configuracion = builder.Configuration.GetRequiredSection("Database");
            var conexion = new MySqlConnectionStringBuilder
            {
                Server = LeerCampo(configuracion, "Host"),
                Port = configuracion.GetValue<uint>("Port", 3306),
                Database = LeerCampo(configuracion, "Name"),
                UserID = LeerCampo(configuracion, "User"),
                Password = LeerCampo(configuracion, "Password"),
                DateTimeKind = MySqlDateTimeKind.Utc
            };
            return new MySqlDataSourceBuilder(conexion.ConnectionString).Build();
        });
        builder.Services.AddScoped<FichasAdministrativasMySql>();
        builder.Services.AddScoped<IFichasAdministrativas>(s => s.GetRequiredService<FichasAdministrativasMySql>());
        builder.Services.AddScoped<IConsultaFichasUsuarios>(s => s.GetRequiredService<FichasAdministrativasMySql>());
        builder.Services.AddScoped<IAuditoriaAdministrativa, AuditoriaAdministrativaMySql>();
        builder.Services.AddScoped<IAltasUsuarios, AltasUsuariosMySql>();
        builder.Services.AddScoped<IAdministracionUsuarios, AdministracionUsuariosMySql>();
    }

    internal static async Task VerificarConexionAsync(IServiceProvider servicios)
    {
        const string consulta = "SELECT 1;";
        var origen = servicios.GetRequiredService<MySqlDataSource>();
        await using var conexion = await origen.OpenConnectionAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        await comando.ExecuteScalarAsync();
    }

    private static string LeerCampo(IConfiguration configuracion, string nombre) =>
        configuracion[nombre] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Database:{nombre} es obligatorio.");
}
