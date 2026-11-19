using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Servicios;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<ServicioInput>(configuracion, "api/servicios/servicios");
