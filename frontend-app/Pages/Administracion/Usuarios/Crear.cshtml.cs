using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class CrearModel(ClienteUsuarios cliente, ReintentosUsuarios reintentos) : PageModel
{
    [BindProperty]
    public UsuarioCreateInput Input { get; set; } = new();

    [BindProperty]
    public Guid Clave { get; set; }

    [BindProperty]
    public string? Reintento { get; set; }

    public string? Error { get; private set; }

    public bool PuedeReintentar => Reintento is not null;

    public void OnGet() => Clave = Guid.NewGuid();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Reintento is not null)
        {
            var intento = reintentos.Recuperar<UsuarioCreateInput>(User, "alta", Reintento);
            if (intento is null)
            {
                return BadRequest("El reintento ya no es válido. Consulte la cuenta antes de iniciar otra operación.");
            }

            Clave = intento.Clave;
            Input = intento.Datos;
            ModelState.Clear();
        }

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
            var correoSolicitado = creado.EstadoInvitacion == "correoSolicitado";
            TempData[correoSolicitado ? "UsuariosMensaje" : "UsuariosAviso"] = creado.EstadoInvitacion switch
            {
                "correoSolicitado" => $"Se creó el usuario {creado.Username}. Keycloak aceptó la solicitud de invitación. El destinatario deberá abrir el correo, verificar su dirección y elegir su contraseña.",
                "envioIncierto" => $"Se creó el usuario {creado.Username}. El envío quedó sin confirmar. Revise el estado de la cuenta antes de reenviar la invitación.",
                _ => $"Se creó el usuario {creado.Username}, pero la invitación quedó pendiente. Reenvíela desde el listado."
            };
            if (!correoSolicitado)
            {
                TempData["UsuariosAvisoId"] = creado.Id;
            }

            return RedirectToPage("./Index");
        }

        Error = respuesta.Mensaje;
        Reintento = respuesta.ConservarClave ? reintentos.Guardar(User, "alta", Clave, Input)
            : respuesta.Estado is 401 or 403 ? Reintento : null;
        ModelState.Remove(nameof(Reintento));
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
