using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Categorias;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<CategoriaServicioInput>(configuracion, "api/servicios/categorias");
