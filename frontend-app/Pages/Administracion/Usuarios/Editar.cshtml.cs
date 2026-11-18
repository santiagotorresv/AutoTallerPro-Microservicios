using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class EditarModel : PageModel
{
    public UsuarioEditInput Input { get; } = new();

    public void OnGet()
    {
    }
}
