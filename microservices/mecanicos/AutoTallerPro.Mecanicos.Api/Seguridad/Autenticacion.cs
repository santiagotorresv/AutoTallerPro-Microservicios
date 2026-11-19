using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
namespace AutoTallerPro.Mecanicos.Api.Seguridad;

internal static class Autenticacion
{
    internal static void AgregarSeguridad(this WebApplicationBuilder builder)
    {
        var autoridad = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority es obligatorio.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opciones =>
        {
            opciones.Authority = autoridad;
            opciones.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"]
                ?? $"{autoridad.TrimEnd('/')}/.well-known/openid-configuration";
            opciones.RequireHttpsMetadata = !builder.Environment.IsDevelopment()
                && !builder.Configuration.GetValue<bool>("Authentication:PermitirMetadataHttpLocal");
            opciones.MapInboundClaims = false;
            opciones.IncludeErrorDetails = false;
            opciones.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = autoridad,
                IssuerValidator = (emisor, _, _) => emisor == autoridad ? emisor : throw new SecurityTokenInvalidIssuerException(),
                ValidateAudience = true, ValidAudience = builder.Configuration["Authentication:Audience"],
                IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.Zero
            };
            opciones.Events = new JwtBearerEvents { OnTokenValidated = contexto =>
            {
                if (contexto.SecurityToken is not JsonWebToken jwt ||
                    !jwt.TryGetPayloadValue<string>("typ", out var tipo) || tipo != "Bearer" ||
                    contexto.Principal?.Identity is not ClaimsIdentity identidad)
                {
                    contexto.Fail("Se requiere un access token de Keycloak.");
                    return Task.CompletedTask;
                }
                foreach (var rol in identidad.FindAll(identidad.RoleClaimType).ToArray()) identidad.RemoveClaim(rol);
                if (jwt.TryGetPayloadValue<JsonElement>("realm_access", out var acceso) &&
                    acceso.ValueKind == JsonValueKind.Object && acceso.TryGetProperty("roles", out var roles) &&
                    roles.ValueKind == JsonValueKind.Array)
                    foreach (var rol in roles.EnumerateArray())
                        if (rol.ValueKind == JsonValueKind.String && rol.GetString() is "Administrador" or "Recepcionista")
                            identidad.AddClaim(new Claim(identidad.RoleClaimType, rol.GetString()!));
                return Task.CompletedTask;
            }};
        });
        builder.Services.AddAuthorizationBuilder().AddPolicy("PersonalAutorizado", politica =>
            politica.RequireAuthenticatedUser().RequireRole("Administrador", "Recepcionista"));
    }
}
