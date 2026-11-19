using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class IndexModel(ClienteUsuarios cliente) : PageModel
{
    public const int TamanoPagina = 10;

    [BindProperty(SupportsGet = true)]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RolFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActivoFiltro { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Pagina { get; set; } = 1;

    public PaginaUsuarios? Resultado { get; private set; }

    public ResumenUsuarios? Resumen { get; private set; }

    public string? Error { get; private set; }

    public AccionPendiente? Pendiente { get; private set; }

    public (int Id, int Segundos)? EsperaReenvio { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostAccionAsync(int id, string accion, Guid clave, string? usuario)
    {
        if (clave == Guid.Empty)
        {
            return BadRequest();
        }

        RespuestaUsuarios<bool>? respuesta = accion switch
        {
            "desactivar" => await cliente.DesactivarAsync(id, clave),
            "reactivar" => await cliente.ReactivarAsync(id, clave),
            "reenviar" => await cliente.ReenviarInvitacionAsync(id, clave),
            _ => null
        };
        if (respuesta is null)
        {
            return BadRequest();
        }

        if (respuesta.Exito)
        {
            TempData["UsuariosMensaje"] = accion switch
            {
                "desactivar" => $"Se desactivó la cuenta {usuario}. Sus sesiones abiertas se cerrarán.",
                "reactivar" => $"Se reactivó la cuenta {usuario}.",
                _ => $"Se reenvió la invitación a {usuario}."
            };
            return RedirectToPage(new { Busqueda, RolFiltro, ActivoFiltro, Pagina });
        }

        Error = respuesta.Mensaje;
        if (respuesta.ConservarClave)
        {
            Pendiente = new AccionPendiente(id, accion, clave, usuario);
        }

        if (respuesta.EsperaSegundos is { } segundos)
        {
            EsperaReenvio = (id, segundos);
        }

        await CargarAsync();
        return Page();
    }

    private async Task CargarAsync()
    {
        Pagina = Math.Max(1, Pagina);
        var listado = await cliente.ListarAsync(Busqueda, RolFiltro, ActivoFiltro, Pagina, TamanoPagina);
        if (listado.Exito)
        {
            Resultado = listado.Valor;
        }
        else
        {
            Error ??= listado.Mensaje;
        }

        var resumen = await cliente.ResumenAsync();
        Resumen = resumen.Valor;
    }

    public sealed record AccionPendiente(int Id, string Accion, Guid Clave, string? Usuario);
}
