using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Mecanicos;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<MecanicoInput>(configuracion, "api/mecanicos/mecanicos");
