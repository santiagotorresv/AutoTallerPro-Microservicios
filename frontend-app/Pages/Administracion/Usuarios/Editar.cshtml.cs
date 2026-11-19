using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class EditarModel(ClienteUsuarios cliente) : PageModel
{
    [BindProperty]
    public UsuarioEditInput Input { get; set; } = new();

    [BindProperty]
    public Guid Clave { get; set; }

    public UsuarioApi? Usuario { get; private set; }

    public string? Error { get; private set; }

    public bool PuedeReintentar { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var respuesta = await cliente.DetalleAsync(id);
        if (respuesta.Estado == 404)
        {
            TempData["UsuariosMensaje"] = respuesta.Mensaje;
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
            Rol = Usuario.Rol,
            Activo = Usuario.Activo
        };
        Clave = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
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
            TempData["UsuariosMensaje"] = $"Se guardaron los cambios de {Input.Username}.";
            return RedirectToPage("./Index");
        }

        if (respuesta.Estado == 404)
        {
            TempData["UsuariosMensaje"] = respuesta.Mensaje;
            return RedirectToPage("./Index");
        }

        Error = respuesta.Mensaje;
        PuedeReintentar = respuesta.ConservarClave;
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
