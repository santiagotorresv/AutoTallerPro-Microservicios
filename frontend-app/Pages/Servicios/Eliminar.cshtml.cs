using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Servicios;
public sealed class EliminarModel(IConfiguration configuracion) : EliminarModel<ServicioInput>(configuracion, "api/servicios/servicios");
