using AutoTallerPro.Frontend.Seguridad;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Cuenta;

public sealed class CambiarContrasenaModel : PageModel
{
    public string? Estado { get; private set; }

    public void OnGet(string? estado) => Estado = estado;

    // Keycloak pide la contraseña actual (max_age=0) y luego la nueva dos veces con la política del realm.
    public IActionResult OnPost()
    {
        var propiedades = new AuthenticationProperties { RedirectUri = Url.Page("/Cuenta/CambiarContrasena") };
        propiedades.Items[SesionKeycloak.AccionKeycloak] = "UPDATE_PASSWORD";
        return Challenge(propiedades, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
