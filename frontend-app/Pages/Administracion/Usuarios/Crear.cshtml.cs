using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class CrearModel(ClienteUsuarios cliente) : PageModel
{
    [BindProperty]
    public UsuarioCreateInput Input { get; set; } = new();

    // Idempotency-Key del alta: se conserva mientras el resultado sea incierto.
    [BindProperty]
    public Guid Clave { get; set; }

    public string? Error { get; private set; }

    public bool PuedeReintentar { get; private set; }

    public void OnGet() => Clave = Guid.NewGuid();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Clave == Guid.Empty)
        {
            NuevaClave();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var respuesta = await cliente.CrearAsync(Input.Email.Trim(), Input.Rol, Input.Nombre.Trim(), Input.Apellido.Trim(), Clave);
        if (respuesta.Exito && respuesta.Valor is { } creado)
        {
            TempData["UsuariosMensaje"] = creado.EstadoInvitacion switch
            {
                "correoSolicitado" => $"Se creó el usuario {creado.Username}. Se envió una invitación a {Input.Email.Trim()} para verificar el correo y elegir la contraseña.",
                "envioIncierto" => $"Se creó el usuario {creado.Username}. El correo de invitación pudo haberse enviado; revise la bandeja antes de reenviarlo.",
                _ => $"Se creó el usuario {creado.Username}, pero la invitación quedó pendiente. Reenvíela desde el listado."
            };
            return RedirectToPage("./Index");
        }

        Error = respuesta.Mensaje;
        PuedeReintentar = respuesta.ConservarClave;
        if (!PuedeReintentar)
        {
            NuevaClave();
        }

        return Page();
    }

    private void NuevaClave()
    {
        Clave = Guid.NewGuid();
        ModelState.Remove(nameof(Clave));
    }
}
