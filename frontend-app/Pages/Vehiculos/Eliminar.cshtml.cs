using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Vehiculos;
public sealed class EliminarModel(IConfiguration configuracion) : EliminarModel<VehiculoInput>(configuracion, "api/clientes-vehiculos/vehiculos");
