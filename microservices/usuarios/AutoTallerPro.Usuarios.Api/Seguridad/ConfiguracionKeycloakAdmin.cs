using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Infrastructure.Keycloak;

namespace AutoTallerPro.Usuarios.Api.Seguridad;

internal static class ConfiguracionKeycloakAdmin
{
    internal static void AddKeycloakAdmin(this WebApplicationBuilder builder)
    {
        var opciones = builder.Configuration.GetSection("KeycloakAdmin").Get<OpcionesKeycloakAdmin>() ?? new();
        opciones.PermitirHttpLocal = builder.Environment.IsDevelopment();
        builder.Services.AddSingleton(opciones);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TokenTecnicoKeycloak>();
        builder.Services.AddHttpClient<IIdentidadesKeycloak, KeycloakIdentidadesCliente>(http =>
            {
                http.Timeout = TimeSpan.FromSeconds(10);
                http.MaxResponseContentBufferSize = 1024 * 1024;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
    }
}
