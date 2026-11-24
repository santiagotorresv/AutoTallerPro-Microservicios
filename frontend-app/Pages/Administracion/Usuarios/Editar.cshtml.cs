using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class EditarModel(ClienteUsuarios cliente, ReintentosUsuarios reintentos) : PageModel
{
    [BindProperty]
    public UsuarioEditInput Input { get; set; } = new();

    [BindProperty]
    public Guid Clave { get; set; }

    [BindProperty]
    public string? Reintento { get; set; }

    public UsuarioApi? Usuario { get; private set; }

    public string? Error { get; private set; }

    public bool PuedeReintentar => Reintento is not null;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var respuesta = await cliente.DetalleAsync(id);
        if (respuesta.Estado == 404)
        {
            TempData["UsuariosAviso"] = respuesta.Mensaje;
            return RedirectToPage("./Index");
        }

        if (!respuesta.Exito || respuesta.Valor is null)
        {
            Error = respuesta.Mensaje;
            return Page();
        }

        Usuario = respuesta.Valor;
        Input = new UsuarioEditInput
        {
            Username = Usuario.Username,
            NombreCompleto = $"{Usuario.Nombre} {Usuario.Apellido}",
            Email = Usuario.Email,
            Rol = Usuario.Roles.Length == 1 ? Usuario.Rol : string.Empty,
            Activo = Usuario.Activo
        };
        Clave = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var operacion = FormattableString.Invariant($"editar:{id}");
        if (Reintento is not null)
        {
            var intento = reintentos.Recuperar<UsuarioEditInput>(User, operacion, Reintento);
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
            await CargarUsuarioAsync(id);
            return Page();
        }

        var respuesta = await cliente.EditarAsync(id, Input.Email.Trim(), Input.Rol, Input.Activo, Clave);
        if (respuesta.Exito)
        {
            TempData["UsuariosMensaje"] = "Se guardaron los cambios de la cuenta.";
            return RedirectToPage("./Index");
        }

        if (respuesta.Estado == 404)
        {
            TempData["UsuariosAviso"] = respuesta.Mensaje;
            return RedirectToPage("./Index");
        }

        Error = respuesta.Mensaje;
        Reintento = respuesta.ConservarClave ? reintentos.Guardar(User, operacion, Clave, Input)
            : respuesta.Estado is 401 or 403 ? Reintento : null;
        ModelState.Remove(nameof(Reintento));
        if (!PuedeReintentar)
        {
            NuevaClave();
        }

        await CargarUsuarioAsync(id);
        return Page();
    }

    private async Task CargarUsuarioAsync(int id) => Usuario = (await cliente.DetalleAsync(id)).Valor;

    private void NuevaClave()
    {
        Clave = Guid.NewGuid();
        ModelState.Remove(nameof(Clave));
    }
}
