using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Categorias;
public sealed class CrearModel(IConfiguration configuracion) : FormularioModel<CategoriaServicioInput>(configuracion, "api/servicios/categorias");
