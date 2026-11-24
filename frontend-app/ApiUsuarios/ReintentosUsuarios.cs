using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AutoTallerPro.Frontend.ApiUsuarios;

public sealed class ReintentosUsuarios(IDataProtectionProvider proteccion)
{
    public string? Guardar<T>(ClaimsPrincipal actor, string operacion, Guid clave, T datos) =>
        Protector(actor, operacion)?.Protect(JsonSerializer.Serialize(new Intento<T>(clave, datos)));

    public Intento<T>? Recuperar<T>(ClaimsPrincipal actor, string operacion, string comprobante)
    {
        try
        {
            var json = Protector(actor, operacion)?.Unprotect(comprobante);
            return json is null ? null : JsonSerializer.Deserialize<Intento<T>>(json);
        }
        catch (Exception error) when (error is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private IDataProtector? Protector(ClaimsPrincipal actor, string operacion) =>
        actor.FindFirstValue("sub") is { Length: > 0 } sujeto
            ? proteccion.CreateProtector("Usuarios.Reintentos", operacion, sujeto)
            : null;

    public sealed record Intento<T>(Guid Clave, T Datos);
}
