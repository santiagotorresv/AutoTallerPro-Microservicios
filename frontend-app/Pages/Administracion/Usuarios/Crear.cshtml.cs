using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class CrearModel : PageModel
{
    public UsuarioCreateInput Input { get; } = new();

    public void OnGet()
    {
    }
}
