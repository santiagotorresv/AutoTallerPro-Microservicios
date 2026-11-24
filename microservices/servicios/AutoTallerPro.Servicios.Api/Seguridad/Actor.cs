using System.Security.Claims;
namespace AutoTallerPro.Servicios.Api.Seguridad;

// Usuario de la bitácora: el username del access token de Keycloak.
internal static class Actor
{
    internal static string? De(ClaimsPrincipal usuario) =>
        usuario.FindFirst("preferred_username")?.Value ?? usuario.FindFirst("sub")?.Value;
}
