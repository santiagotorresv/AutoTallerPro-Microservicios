using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Clientes;
public sealed class EliminarModel(IConfiguration configuracion) : EliminarModel<ClienteInput>(configuracion, "api/clientes-vehiculos/clientes");
