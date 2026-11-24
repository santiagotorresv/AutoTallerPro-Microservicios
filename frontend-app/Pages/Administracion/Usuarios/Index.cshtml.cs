using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class IndexModel(ClienteUsuarios cliente, ReintentosUsuarios reintentos) : PageModel
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
    public string? Reintento { get; private set; }

    public (int Id, int Segundos)? EsperaReenvio { get; private set; }

    public async Task OnGetAsync() => await CargarAsync();

    public async Task<IActionResult> OnPostAccionAsync(int id, string accion, Guid clave, string? usuario, string? reintento)
    {
        if (reintento is not null)
        {
            var intento = reintentos.Recuperar<AccionPendiente>(User, "accion", reintento);
            if (intento is null)
            {
                return BadRequest("El reintento ya no es válido. Consulte la cuenta antes de iniciar otra operación.");
            }

            (id, accion, usuario) = intento.Datos;
            clave = intento.Clave;
        }

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
                _ => "Keycloak aceptó la solicitud de reenvío. El estado de la invitación puede consultarse en el detalle."
            };
            return RedirectToPage(new { Busqueda, RolFiltro, ActivoFiltro, Pagina });
        }

        Error = respuesta.Mensaje;
        if (respuesta.ConservarClave || reintento is not null && (respuesta.Estado is 401 or 403 or 429))
        {
            Pendiente = new AccionPendiente(id, accion, usuario);
            Reintento = reintentos.Guardar(User, "accion", clave, Pendiente);
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
        Error ??= resumen.Mensaje;
    }

    public sealed record AccionPendiente(int Id, string Accion, string? Usuario);
}
