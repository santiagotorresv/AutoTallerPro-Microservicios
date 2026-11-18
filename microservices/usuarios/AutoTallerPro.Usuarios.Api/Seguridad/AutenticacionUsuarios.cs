using System.Security.Claims;
using System.Text.Json;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AutoTallerPro.Usuarios.Api.Seguridad;

internal static class AutenticacionUsuarios
{
    internal const string PersonalAutorizado = "PersonalAutorizado";
    internal const string SoloAdministrador = "SoloAdministrador";

    internal static void AddAutenticacionUsuarios(this WebApplicationBuilder builder)
    {
        string authority = builder.Configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority es obligatorio.");
        string audience = builder.Configuration["Authentication:Audience"]
            ?? throw new InvalidOperationException("Authentication:Audience es obligatorio.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"]
                    ?? $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    IssuerValidator = (issuer, _, _) => string.Equals(issuer, authority, StringComparison.Ordinal)
                        ? issuer
                        : throw new SecurityTokenInvalidIssuerException(),
                    ValidateAudience = true,
                    ValidAudience = audience,
                    IgnoreTrailingSlashWhenValidatingAudience = false,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.PreferredUsername
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = MapearIdentidad };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(PersonalAutorizado, policy => policy.RequireAuthenticatedUser()
                .RequireRole(Roles.Administrador, Roles.Recepcionista))
            .AddPolicy(SoloAdministrador, policy => policy.RequireAuthenticatedUser()
                .RequireRole(Roles.Administrador));
    }

    private static Task MapearIdentidad(TokenValidatedContext context)
    {
        if (context.SecurityToken is not JsonWebToken jwt
            || !jwt.TryGetPayloadValue<string>("typ", out var type)
            || type != "Bearer"
            || context.Principal?.Identity is not ClaimsIdentity identity
            || string.IsNullOrWhiteSpace(jwt.Subject)
            || string.IsNullOrWhiteSpace(identity.Name))
        {
            context.Fail("Se requiere un access token de usuario de Keycloak.");
            return Task.CompletedTask;
        }

        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        if (jwt.TryGetPayloadValue<JsonElement>("realm_access", out var realmAccess)
            && realmAccess.ValueKind == JsonValueKind.Object
            && realmAccess.TryGetProperty("roles", out var roles)
            && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String && role.GetString() is { } name
                    && Roles.EsDeNegocio(name))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, name));
                }
            }
        }

        return Task.CompletedTask;
    }
}
