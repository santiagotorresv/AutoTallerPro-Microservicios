using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Clientes;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<ClienteInput>(configuracion, "api/clientes-vehiculos/clientes");
