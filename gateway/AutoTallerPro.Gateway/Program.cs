using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

const string administrador = "Administrador";
const string recepcionista = "Recepcionista";

var builder = WebApplication.CreateBuilder(args);
var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority es obligatorio.");
var audience = builder.Configuration["Authentication:Audience"]
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
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents { OnTokenValidated = MapRealmRoles };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("PersonalAutorizado", policy => policy.RequireAuthenticatedUser()
        .RequireRole(administrador, recepcionista))
    .AddPolicy("SoloAdministrador", policy => policy.RequireAuthenticatedUser()
        .RequireRole(administrador));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();

static Task MapRealmRoles(TokenValidatedContext context)
{
    if (context.SecurityToken is not JsonWebToken jwt
        || !jwt.TryGetPayloadValue<string>("typ", out var type)
        || type != "Bearer"
        || context.Principal?.Identity is not ClaimsIdentity identity)
    {
        context.Fail("Se requiere un access token de Keycloak.");
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
            if (role.ValueKind == JsonValueKind.String
                && role.GetString() is { } name
                && name is administrador or recepcionista)
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, name));
            }
        }
    }

    return Task.CompletedTask;
}
