using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ClientesApi = AutoTallerPro.ClientesVehiculos.Api.ApiAssembly;
using ServiciosApi = AutoTallerPro.Servicios.Api.ApiAssembly;
using MecanicosApi = AutoTallerPro.Mecanicos.Api.ApiAssembly;
namespace AutoTallerPro.GrupoUno.Tests;

internal static class TokensPrueba
{
    internal const string Emisor = "http://127.0.0.1:8180/realms/autotaller";
    private static readonly RSA rsa = RSA.Create(2048);
    internal static readonly RsaSecurityKey Clave = new(rsa) { KeyId = "pruebas-grupo-1" };
    internal static string Crear(string rol = "Administrador", string emisor = Emisor, string audiencia = "autotaller-api", string tipo = "Bearer", bool expirado = false, bool firmaAjena = false)
    {
        var ahora = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = emisor, ["aud"] = audiencia, ["typ"] = tipo, ["sub"] = "usuario-prueba",
            ["preferred_username"] = "prueba", ["iat"] = ahora.AddMinutes(-10).ToUnixTimeSeconds(),
            ["nbf"] = ahora.AddMinutes(-10).ToUnixTimeSeconds(),
            ["exp"] = (expirado ? ahora.AddMinutes(-1) : ahora.AddMinutes(10)).ToUnixTimeSeconds(),
            ["realm_access"] = new { roles = new[] { rol } },
            ["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] = "Administrador"
        };
        using var ajena = RSA.Create(2048);
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(firmaAjena ? new RsaSecurityKey(ajena) : Clave, SecurityAlgorithms.RsaSha256));
    }
    internal static void Configurar(IServiceCollection servicios)
    {
        servicios.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opciones =>
        {
            opciones.ConfigurationManager = null;
            opciones.Configuration = new OpenIdConnectConfiguration { Issuer = Emisor };
            opciones.Configuration.SigningKeys.Add(Clave);
            opciones.TokenValidationParameters.IssuerSigningKey = Clave;
        });
    }
}
internal sealed class FabricaPrueba<T>(Action<IServiceCollection> configurar) : WebApplicationFactory<T> where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development").ConfigureServices(servicios =>
    { TokensPrueba.Configurar(servicios); configurar(servicios); });
}
public sealed class SeguridadHttpTests
{
    [Theory] [InlineData("clientes")] [InlineData("servicios")] [InlineData("mecanicos")]
    public async Task SaludAnonimaYNegocioRequiereToken(string modulo)
    {
        using var fabrica = CrearFabrica(modulo); using var cliente = fabrica.Cliente;
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/salud")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(fabrica.Ruta)).StatusCode);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(fabrica.Ruta)).StatusCode);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear(rol: "Recepcionista"));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(fabrica.Ruta)).StatusCode);
    }
    [Theory] [InlineData("clientes")] [InlineData("servicios")] [InlineData("mecanicos")]
    public async Task RechazaTokensInvalidosYRolesFalsificados(string modulo)
    {
        using var fabrica = CrearFabrica(modulo); using var cliente = fabrica.Cliente;
        foreach (var token in new[] { "invalido", TokensPrueba.Crear(emisor: "https://otro.example"), TokensPrueba.Crear(audiencia: "otra"), TokensPrueba.Crear(tipo: "ID"), TokensPrueba.Crear(expirado: true), TokensPrueba.Crear(firmaAjena: true) })
        {
            cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(fabrica.Ruta)).StatusCode);
        }
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear(rol: "SinPermiso"));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(fabrica.Ruta)).StatusCode);
    }
    [Fact] public async Task ClientesMapea201400404409YEliminacion()
    {
        using var fabrica = CrearFabrica("clientes"); using var cliente = fabrica.Cliente;
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear());
        var datos = new { documento = "7654321", nombre = "Cliente HTTP", telefono = "76543210", correo = (string?)null, activo = true };
        var creado = await cliente.PostAsJsonAsync("/clientes", datos);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var entidad = await creado.Content.ReadFromJsonAsync<JsonElement>(); var id = entidad.GetProperty("id").GetString();
        Assert.Equal($"/api/clientes-vehiculos/clientes/{id}", creado.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync("/clientes", datos)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/clientes", datos with { nombre = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/clientes/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PutAsJsonAsync($"/clientes/{id}", datos with { nombre = "Cliente actualizado" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.DeleteAsync($"/clientes/{id}")).StatusCode);
    }
    [Fact] public async Task MongoIdInvalidoDevuelve400()
    {
        using var fabrica = CrearFabrica("mecanicos"); using var cliente = fabrica.Cliente;
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear());
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/mecanicos/invalido")).StatusCode);
    }
    private static FabricaCliente CrearFabrica(string modulo)
    {
        return modulo switch
        {
            "clientes" => Crear<ClientesApi>("/clientes", servicios =>
            {
                servicios.RemoveAll<AutoTallerPro.ClientesVehiculos.Application.Puertos.IClienteRepositorio>();
                servicios.AddSingleton<AutoTallerPro.ClientesVehiculos.Application.Puertos.IClienteRepositorio>(new ClienteMemoria());
                servicios.RemoveAll<AutoTallerPro.ClientesVehiculos.Application.Puertos.IVehiculoRepositorio>();
                servicios.AddSingleton<AutoTallerPro.ClientesVehiculos.Application.Puertos.IVehiculoRepositorio>(new VehiculoMemoria());
            }),
            "servicios" => Crear<ServiciosApi>("/servicios", servicios =>
            {
                servicios.RemoveAll<AutoTallerPro.Servicios.Application.Puertos.IServicioRepositorio>();
                servicios.AddSingleton<AutoTallerPro.Servicios.Application.Puertos.IServicioRepositorio>(new ServicioMemoria());
                servicios.RemoveAll<AutoTallerPro.Servicios.Application.Puertos.ICategoriaServicioRepositorio>();
                servicios.AddSingleton<AutoTallerPro.Servicios.Application.Puertos.ICategoriaServicioRepositorio>(new CategoriaServicioMemoria());
            }),
            _ => Crear<MecanicosApi>("/mecanicos", servicios =>
            {
                servicios.RemoveAll<AutoTallerPro.Mecanicos.Application.Puertos.IMecanicoRepositorio>();
                servicios.AddSingleton<AutoTallerPro.Mecanicos.Application.Puertos.IMecanicoRepositorio>(new MecanicoMemoria());
            })
        };
    }
    private static FabricaCliente Crear<T>(string ruta, Action<IServiceCollection> configurar) where T : class
    { var fabrica = new FabricaPrueba<T>(configurar); return new(fabrica, fabrica.CreateClient(), ruta); }
    private sealed record FabricaCliente(IDisposable Fabrica, HttpClient Cliente, string Ruta) : IDisposable { public void Dispose() => Fabrica.Dispose(); }
}
