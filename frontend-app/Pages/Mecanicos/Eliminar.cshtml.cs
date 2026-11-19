using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Mecanicos;
public sealed class EliminarModel(IConfiguration configuracion) : EliminarModel<MecanicoInput>(configuracion, "api/mecanicos/mecanicos");
