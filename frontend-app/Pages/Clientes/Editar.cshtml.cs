using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Clientes;
public sealed class EditarModel(IConfiguration configuracion) : FormularioModel<ClienteInput>(configuracion, "api/clientes-vehiculos/clientes");
