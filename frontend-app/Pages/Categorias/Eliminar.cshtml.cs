using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Categorias;
public sealed class EliminarModel(IConfiguration configuracion) : EliminarModel<CategoriaServicioInput>(configuracion, "api/servicios/categorias");
