using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Mecanicos;
public sealed class CrearModel(IConfiguration configuracion) : FormularioModel<MecanicoInput>(configuracion, "api/mecanicos/mecanicos");
