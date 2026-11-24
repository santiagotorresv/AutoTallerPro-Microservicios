using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Servicios;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<ServicioInput>(configuracion, "api/servicios/servicios")
{
    private IReadOnlyDictionary<string, CategoriaServicioInput> categorias = new Dictionary<string, CategoriaServicioInput>();
    public string? ErrorRelacion { get; private set; }
    public string NombreCategoria(string id) => categorias.TryGetValue(id, out var categoria) ? categoria.Nombre : "No disponible";
    protected override async Task CargarRelacionesAsync()
    {
        var resultado = await new ClienteGateway(Configuracion, HttpContext).EnviarAsync<List<CategoriaServicioInput>>(HttpMethod.Get, "api/servicios/categorias");
        if (resultado.Exito) categorias = (resultado.Valor ?? []).ToDictionary(x => x.Id);
        else ErrorRelacion = "No se pudieron consultar los nombres de las categorías. " + resultado.Error;
    }
}
