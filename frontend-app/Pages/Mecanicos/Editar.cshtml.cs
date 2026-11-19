using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Mecanicos;
public sealed class EditarModel(IConfiguration configuracion) : FormularioModel<MecanicoInput>(configuracion, "api/mecanicos/mecanicos");
