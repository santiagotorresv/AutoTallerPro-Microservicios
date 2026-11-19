using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Vehiculos;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<VehiculoInput>(configuracion, "api/clientes-vehiculos/vehiculos");
