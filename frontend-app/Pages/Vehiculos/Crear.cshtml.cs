using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Vehiculos;
public sealed class CrearModel(IConfiguration configuracion) : FormularioModel<VehiculoInput>(configuracion, "api/clientes-vehiculos/vehiculos")
{
    public IReadOnlyList<ClienteInput> Opciones { get; private set; } = [];
    protected override async Task CargarOpcionesAsync()
    {
        var respuesta = await new ClienteGateway(Configuracion, HttpContext).EnviarAsync<List<ClienteInput>>(HttpMethod.Get, "api/clientes-vehiculos/clientes");
        if (respuesta.Exito) Opciones = respuesta.Valor ?? [];
        else ModelState.AddModelError("", respuesta.Error!);
    }
}
