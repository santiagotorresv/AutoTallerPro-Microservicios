using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Categorias;
public sealed class EditarModel(IConfiguration configuracion) : FormularioModel<CategoriaServicioInput>(configuracion, "api/servicios/categorias");
