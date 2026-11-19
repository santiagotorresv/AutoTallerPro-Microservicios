using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
namespace AutoTallerPro.GrupoUno.Tests;
internal static class FlujoHttpPersistencia
{
    internal static async Task Verificar<TApi>(string ruta, Action<IServiceCollection> configurar, object datos, object cambio, object invalido, string campo, string valorEsperado) where TApi : class
    {
        using var fabrica = new FabricaPrueba<TApi>(configurar); using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear());
        var creado = await cliente.PostAsJsonAsync(ruta, datos);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var entidad = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = entidad.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(ruta, datos)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{ruta}/{id}")).StatusCode);
        var antes = await cliente.GetFromJsonAsync<JsonElement[]>(ruta);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, invalido)).StatusCode);
        Assert.Equal(antes!.Length, (await cliente.GetFromJsonAsync<JsonElement[]>(ruta))!.Length);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PutAsJsonAsync($"{ruta}/{id}", cambio)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync($"{ruta}/{id}", invalido)).StatusCode);
        var refrescado = await cliente.GetFromJsonAsync<JsonElement>($"{ruta}/{id}");
        Assert.Equal(valorEsperado, refrescado.GetProperty(campo).ToString());
        var listado = await cliente.GetFromJsonAsync<JsonElement[]>(ruta + "?ordenar=fecha&descendente=true");
        Assert.Contains(listado!, x => x.GetProperty("id").GetString() == id);
        Assert.Equal(HttpStatusCode.OK, (await cliente.DeleteAsync($"{ruta}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"{ruta}/{id}")).StatusCode);
    }
}
