using AutoTallerPro.Frontend.GrupoUno;
namespace AutoTallerPro.Frontend.Pages.Vehiculos;
public sealed class IndexModel(IConfiguration configuracion) : ListaModel<VehiculoInput>(configuracion, "api/clientes-vehiculos/vehiculos")
{
    private IReadOnlyDictionary<string, ClienteInput> clientes = new Dictionary<string, ClienteInput>();
    public string? ErrorRelacion { get; private set; }
    public string NombreCliente(string id) => clientes.TryGetValue(id, out var cliente) ? cliente.Nombre : "No disponible";
    protected override async Task CargarRelacionesAsync()
    {
        var resultado = await new ClienteGateway(Configuracion, HttpContext).EnviarAsync<List<ClienteInput>>(HttpMethod.Get, "api/clientes-vehiculos/clientes");
        if (resultado.Exito) clientes = (resultado.Valor ?? []).ToDictionary(x => x.Id);
        else ErrorRelacion = "No se pudieron consultar los nombres de los propietarios. " + resultado.Error;
    }
}
