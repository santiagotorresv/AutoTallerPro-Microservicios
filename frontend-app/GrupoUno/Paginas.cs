using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AutoTallerPro.Frontend.GrupoUno;

public abstract class ListaModel<T>(IConfiguration configuracion, string ruta) : PageModel
{
    public IReadOnlyList<T> Registros { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Buscar { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ordenar { get; set; }
    [BindProperty(SupportsGet = true)] public bool Descendente { get; set; }
    public string? Error { get; private set; }
    public async Task OnGetAsync()
    {
        var url = ruta + "?buscar=" + Uri.EscapeDataString(Buscar ?? "") + "&ordenar=" + Uri.EscapeDataString(Ordenar ?? "") + "&descendente=" + Descendente.ToString().ToLowerInvariant();
        var resultado = await new ClienteGateway(configuracion, HttpContext).EnviarAsync<List<T>>(HttpMethod.Get, url);
        if (resultado.Exito) Registros = resultado.Valor ?? [];
        else Error = resultado.Error;
    }
}
public abstract class FormularioModel<T>(IConfiguration configuracion, string ruta) : PageModel where T : new()
{
    protected IConfiguration Configuracion => configuracion;
    [BindProperty] public T Input { get; set; } = new();
    public string? Error { get; private set; }
    protected virtual Task CargarOpcionesAsync() => Task.CompletedTask;
    public async Task<IActionResult> OnGetAsync(string? id)
    {
        if (id is not null)
        {
            var resultado = await new ClienteGateway(configuracion, HttpContext).EnviarAsync<T>(HttpMethod.Get, ruta + "/" + Uri.EscapeDataString(id));
            if (resultado.Exito && resultado.Valor is not null) Input = resultado.Valor;
            else Error = resultado.Error;
        }
        await CargarOpcionesAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string? id)
    {
        if (ModelState.IsValid)
        {
            var resultado = await new ClienteGateway(configuracion, HttpContext).EnviarAsync<T>(id is null ? HttpMethod.Post : HttpMethod.Put,
                ruta + (id is null ? "" : "/" + Uri.EscapeDataString(id)), Input);
            if (resultado.Exito) { TempData["GrupoUnoMensaje"] = "Los datos se guardaron correctamente."; return RedirectToPage("./Index"); }
            Error = resultado.Error;
        }
        await CargarOpcionesAsync(); return Page();
    }
}
public abstract class EliminarModel<T>(IConfiguration configuracion, string ruta) : PageModel
{
    public T? Registro { get; private set; }
    public string? Error { get; private set; }
    public async Task OnGetAsync(string id)
    {
        var resultado = await new ClienteGateway(configuracion, HttpContext).EnviarAsync<T>(HttpMethod.Get, ruta + "/" + Uri.EscapeDataString(id));
        Registro = resultado.Valor; Error = resultado.Error;
    }
    public async Task<IActionResult> OnPostAsync(string id)
    {
        var resultado = await new ClienteGateway(configuracion, HttpContext).EnviarAsync<bool>(HttpMethod.Delete, ruta + "/" + Uri.EscapeDataString(id));
        if (resultado.Exito) { TempData["GrupoUnoMensaje"] = "El registro se eliminó correctamente."; return RedirectToPage("./Index"); }
        Error = resultado.Error; return Page();
    }
}
