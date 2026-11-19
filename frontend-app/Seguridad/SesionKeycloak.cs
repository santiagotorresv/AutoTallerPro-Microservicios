using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AutoTallerPro.Frontend.Seguridad;

public static class SesionKeycloak
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string SoloAdministrador = "SoloAdministrador";
    public const string AccionKeycloak = "kc_action";
    private const string TipoRol = "role";

    public static void AddSesionKeycloak(this WebApplicationBuilder builder)
    {
        var configuracion = builder.Configuration.GetSection("Autenticacion");

        builder.Services.AddAuthentication(opciones =>
            {
                opciones.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                opciones.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(opciones =>
            {
                opciones.Cookie.Name = "AutoTallerPro.Sesion";
                opciones.Cookie.HttpOnly = true;
                opciones.Cookie.SameSite = SameSiteMode.Lax;
                opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opciones.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                opciones.SlidingExpiration = true;
                opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
                opciones.Events.OnValidatePrincipal = RenovarTokensAsync;
            })
            .AddOpenIdConnect(opciones =>
            {
                opciones.Authority = configuracion["Authority"];
                if (!string.IsNullOrWhiteSpace(configuracion["MetadataAddress"]))
                {
                    opciones.MetadataAddress = configuracion["MetadataAddress"];
                }

                opciones.ClientId = configuracion["ClientId"];
                opciones.ClientSecret = configuracion["ClientSecret"];
                opciones.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                opciones.ResponseType = "code";
                opciones.UsePkce = true;
                opciones.SaveTokens = true;
                opciones.MapInboundClaims = false;
                opciones.Scope.Clear();
                opciones.Scope.Add("openid");
                opciones.Scope.Add("profile");
                opciones.Scope.Add("email");
                opciones.TokenValidationParameters.NameClaimType = "preferred_username";
                opciones.TokenValidationParameters.RoleClaimType = TipoRol;
                opciones.CorrelationCookie.SameSite = SameSiteMode.Lax;
                opciones.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opciones.NonceCookie.SameSite = SameSiteMode.Lax;
                opciones.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opciones.Events.OnRedirectToIdentityProvider = PrepararRedireccion;
                opciones.Events.OnTokenValidated = SesionIniciada;
                opciones.Events.OnRemoteFailure = InicioFallido;
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(SoloAdministrador, politica => politica.RequireAuthenticatedUser().RequireRole(Administrador));
    }

    private static Task PrepararRedireccion(RedirectContext contexto)
    {
        contexto.ProtocolMessage.UiLocales = "es";
        if (contexto.Properties.Items.TryGetValue(AccionKeycloak, out var accion) && accion is not null)
        {
            contexto.ProtocolMessage.SetParameter(AccionKeycloak, accion);
            contexto.ProtocolMessage.MaxAge = "0";
        }

        return Task.CompletedTask;
    }

    private static Task SesionIniciada(TokenValidatedContext contexto)
    {
        if (contexto.Principal?.Identity is ClaimsIdentity identidad)
        {
            AsignarRoles(identidad, contexto.TokenEndpointResponse?.AccessToken);
        }

        var estadoAccion = contexto.ProtocolMessage.GetParameter("kc_action_status");
        if (estadoAccion is not null)
        {
            contexto.Properties!.RedirectUri = QueryHelpers.AddQueryString(
                contexto.Properties.RedirectUri ?? "/", "estado", estadoAccion);
        }

        return Task.CompletedTask;
    }

    private static Task InicioFallido(RemoteFailureContext contexto)
    {
        var solicitud = contexto.Request;
        string? estadoAccion = solicitud.Query["kc_action_status"];
        if (estadoAccion is null && solicitud.HasFormContentType)
        {
            estadoAccion = solicitud.Form["kc_action_status"];
        }

        contexto.HandleResponse();
        contexto.Response.Redirect(estadoAccion is null
            ? "/Cuenta/ErrorSesion"
            : QueryHelpers.AddQueryString("/Cuenta/CambiarContrasena", "estado", estadoAccion));
        return Task.CompletedTask;
    }

    // Keycloak caduca el access token a los 5 minutos; se renueva con el refresh token antes de llamar a las APIs.
    private static async Task RenovarTokensAsync(CookieValidatePrincipalContext contexto)
    {
        var expira = contexto.Properties.GetTokenValue("expires_at");
        if (expira is null ||
            DateTimeOffset.Parse(expira, CultureInfo.InvariantCulture) > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return;
        }

        var tokens = await PedirTokensNuevosAsync(contexto);
        if (tokens is not { } nuevos)
        {
            contexto.RejectPrincipal();
            await contexto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var accessToken = nuevos.GetProperty("access_token").GetString()!;
        contexto.Properties.UpdateTokenValue("access_token", accessToken);
        if (nuevos.TryGetProperty("refresh_token", out var refreshToken))
        {
            contexto.Properties.UpdateTokenValue("refresh_token", refreshToken.GetString()!);
        }

        if (nuevos.TryGetProperty("id_token", out var idToken))
        {
            contexto.Properties.UpdateTokenValue("id_token", idToken.GetString()!);
        }

        var vence = DateTimeOffset.UtcNow.AddSeconds(nuevos.GetProperty("expires_in").GetInt32());
        contexto.Properties.UpdateTokenValue("expires_at", vence.ToString("o", CultureInfo.InvariantCulture));
        if (contexto.Principal?.Identity is ClaimsIdentity identidad)
        {
            AsignarRoles(identidad, accessToken);
        }

        contexto.ShouldRenew = true;
    }

    private static async Task<JsonElement?> PedirTokensNuevosAsync(CookieValidatePrincipalContext contexto)
    {
        var refreshToken = contexto.Properties.GetTokenValue("refresh_token");
        var oidc = contexto.HttpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        if (refreshToken is null || oidc.ConfigurationManager is null)
        {
            return null;
        }

        try
        {
            var cancelacion = contexto.HttpContext.RequestAborted;
            var metadatos = await oidc.ConfigurationManager.GetConfigurationAsync(cancelacion);
            using var respuesta = await oidc.Backchannel.PostAsync(metadatos.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = oidc.ClientId!,
                    ["client_secret"] = oidc.ClientSecret ?? string.Empty
                }), cancelacion);
            return respuesta.IsSuccessStatusCode
                ? await respuesta.Content.ReadFromJsonAsync<JsonElement>(cancelacion)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    // El access token llega del endpoint de tokens por el canal trasero; solo se leen sus roles de negocio.
    private static void AsignarRoles(ClaimsIdentity identidad, string? accessToken)
    {
        foreach (var rol in identidad.FindAll(TipoRol).ToList())
        {
            identidad.RemoveClaim(rol);
        }

        if (accessToken is null)
        {
            return;
        }

        var token = new JsonWebToken(accessToken);
        if (token.TryGetPayloadValue<JsonElement>("realm_access", out var acceso)
            && acceso.ValueKind == JsonValueKind.Object
            && acceso.TryGetProperty("roles", out var roles)
            && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var rol in roles.EnumerateArray())
            {
                if (rol.GetString() is Administrador or Recepcionista)
                {
                    identidad.AddClaim(new Claim(TipoRol, rol.GetString()!));
                }
            }
        }
    }
}
