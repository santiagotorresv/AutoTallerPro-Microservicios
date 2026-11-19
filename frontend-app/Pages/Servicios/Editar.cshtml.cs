using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Servicios;
public sealed class EditarModel(IConfiguration configuracion) : FormularioModel<ServicioInput>(configuracion, "api/servicios/servicios")
{
    public IReadOnlyList<CategoriaServicioInput> Opciones { get; private set; } = [];
    protected override async Task CargarOpcionesAsync()
    {
        var respuesta = await new ClienteGateway(Configuracion, HttpContext).EnviarAsync<List<CategoriaServicioInput>>(HttpMethod.Get, "api/servicios/categorias");
        if (respuesta.Exito) Opciones = respuesta.Valor ?? [];
        else ModelState.AddModelError("", respuesta.Error!);
    }
}
