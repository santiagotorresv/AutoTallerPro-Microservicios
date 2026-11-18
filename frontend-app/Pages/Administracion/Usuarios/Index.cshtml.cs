using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class IndexModel : PageModel
{
    public IReadOnlyList<UsuarioListViewModel> Usuarios { get; } = [];

    public string? Busqueda { get; set; }

    public string? RolFiltro { get; set; }

    public bool? ActivoFiltro { get; set; }

    public void OnGet()
    {
    }
}
