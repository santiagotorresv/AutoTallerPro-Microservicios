using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Clientes = AutoTallerPro.ClientesVehiculos;
using Catalogo = AutoTallerPro.Servicios;
using Personal = AutoTallerPro.Mecanicos;
using AutoTallerPro.Frontend.GrupoUno;
using AutoTallerPro.GrupoUno.GatewayVerificacion;
namespace AutoTallerPro.GrupoUno.Tests;

internal sealed class SesionPrueba(IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var propiedades = new AuthenticationProperties();
        propiedades.StoreTokens([new() { Name = "access_token", Value = TokensPrueba.Crear() }]);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Pruebas")], Scheme.Name)), propiedades, Scheme.Name)));
    }
}
public sealed class GatewayRazorTests
{
    [Fact] public async Task RazorGatewayYApisCompletanCrudConSesionDePrueba()
    {
        var reales = Environment.GetEnvironmentVariable("GRUPO1_PRUEBAS_AISLADAS") == "1";
        string Recurso(string clave) => Environment.GetEnvironmentVariable(clave) ?? throw new InvalidOperationException($"Falta {clave}.");
        if (reales && (!new Npgsql.NpgsqlConnectionStringBuilder(Recurso("GRUPO1_POSTGRES")).Database!.StartsWith("test_grupo1_", StringComparison.Ordinal)
            || !new MySqlConnector.MySqlConnectionStringBuilder(Recurso("GRUPO1_MYSQL")).Database.StartsWith("test_grupo1_", StringComparison.Ordinal)
            || !new MongoDB.Driver.MongoUrl(Recurso("GRUPO1_MONGO")).DatabaseName.StartsWith("test_grupo1_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Solo se admiten bases de pruebas propias.");
        Clientes.Application.Puertos.IClienteRepositorio repoClientes = reales ? new Clientes.Infrastructure.Persistencia.ClienteRepositorio(new(Recurso("GRUPO1_POSTGRES"))) : new ClienteMemoria();
        Clientes.Application.Puertos.IVehiculoRepositorio repoVehiculos = reales ? new Clientes.Infrastructure.Persistencia.VehiculoRepositorio(new(Recurso("GRUPO1_POSTGRES"))) : new VehiculoMemoria();
        Catalogo.Application.Puertos.ICategoriaServicioRepositorio repoCategorias = reales ? new Catalogo.Infrastructure.Persistencia.CategoriaServicioRepositorio(new(Recurso("GRUPO1_MYSQL"))) : new CategoriaServicioMemoria();
        Catalogo.Application.Puertos.IServicioRepositorio repoServicios = reales ? new Catalogo.Infrastructure.Persistencia.ServicioRepositorio(new(Recurso("GRUPO1_MYSQL"))) : new ServicioMemoria();
        Personal.Application.Puertos.IMecanicoRepositorio repoMecanicos = reales ? new Personal.Infrastructure.Persistencia.MecanicoRepositorio(new MongoDB.Driver.MongoClient(Recurso("GRUPO1_MONGO")).GetDatabase(new MongoDB.Driver.MongoUrl(Recurso("GRUPO1_MONGO")).DatabaseName)) : new MecanicoMemoria();
        using var clientes = new FabricaPrueba<Clientes.Api.ApiAssembly>(sp =>
        {
            sp.RemoveAll<Clientes.Application.Puertos.IClienteRepositorio>(); sp.AddSingleton<Clientes.Application.Puertos.IClienteRepositorio>(repoClientes);
            sp.RemoveAll<Clientes.Application.Puertos.IVehiculoRepositorio>(); sp.AddSingleton<Clientes.Application.Puertos.IVehiculoRepositorio>(repoVehiculos);
        });
        using var catalogo = new FabricaPrueba<Catalogo.Api.ApiAssembly>(sp =>
        {
            sp.RemoveAll<Catalogo.Application.Puertos.ICategoriaServicioRepositorio>(); sp.AddSingleton<Catalogo.Application.Puertos.ICategoriaServicioRepositorio>(repoCategorias);
            sp.RemoveAll<Catalogo.Application.Puertos.IServicioRepositorio>(); sp.AddSingleton<Catalogo.Application.Puertos.IServicioRepositorio>(repoServicios);
        });
        using var personal = new FabricaPrueba<Personal.Api.ApiAssembly>(sp =>
        {
            sp.RemoveAll<Personal.Application.Puertos.IMecanicoRepositorio>(); sp.AddSingleton<Personal.Application.Puertos.IMecanicoRepositorio>(repoMecanicos);
        });
        clientes.UseKestrel(0); catalogo.UseKestrel(0); personal.UseKestrel(0);
        using var clienteDirecto = clientes.CreateClient(); using var catalogoDirecto = catalogo.CreateClient(); using var personalDirecto = personal.CreateClient();
        using var gateway = new WebApplicationFactory<EntradaGateway>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseContentRoot(Path.Combine(Raiz(), "gateway/AutoTallerPro.Gateway"))
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddJsonFile(Path.Combine(Raiz(), "gateway/AutoTallerPro.Gateway/appsettings.json"));
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:ms-clientes:Destinations:principal:Address"] = clienteDirecto.BaseAddress!.ToString(),
                    ["ReverseProxy:Clusters:ms-servicios:Destinations:principal:Address"] = catalogoDirecto.BaseAddress!.ToString(),
                    ["ReverseProxy:Clusters:ms-mecanicos:Destinations:principal:Address"] = personalDirecto.BaseAddress!.ToString()
                });
            }).ConfigureServices(TokensPrueba.Configurar));
        gateway.UseKestrel(0);
        using var proxy = gateway.CreateClient();
        foreach (var modulo in new[] { "clientes-vehiculos", "servicios", "mecanicos" })
            Assert.Equal(HttpStatusCode.OK, (await proxy.GetAsync($"/api/{modulo}/salud")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await proxy.GetAsync("/api/clientes-vehiculos/clientes")).StatusCode);
        proxy.DefaultRequestHeaders.Authorization = new("Bearer", TokensPrueba.Crear());
        var propietario = await (await proxy.PostAsJsonAsync("/api/clientes-vehiculos/clientes", new Clientes.Application.DatosCliente("PADRE123", "Propietario", null, null))).Content.ReadFromJsonAsync<JsonElement>();
        var categoria = await (await proxy.PostAsJsonAsync("/api/servicios/categorias", new Catalogo.Application.DatosCategoriaServicio("Categoría padre", null))).Content.ReadFromJsonAsync<JsonElement>();
        using var frontend = new WebApplicationFactory<FrontendAssembly>().WithWebHostBuilder(builder => builder
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["GrupoUno:GatewayUrl"] = proxy.BaseAddress!.ToString() }))
            .ConfigureServices(sp => sp.AddAuthentication("SesionPrueba").AddScheme<AuthenticationSchemeOptions, SesionPrueba>("SesionPrueba", _ => { })));
        using var navegador = frontend.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Formulario(navegador, proxy, "Clientes", "api/clientes-vehiculos/clientes", new() { ["Documento"] = "RAZOR123", ["Nombre"] = "Cliente Razor", ["Activo"] = "true" }, "Nombre", "Cliente actualizado");
        await Formulario(navegador, proxy, "Vehiculos", "api/clientes-vehiculos/vehiculos", new() { ["ClienteId"] = propietario.GetProperty("id").GetString()!, ["Placa"] = "5678XYZ", ["Marca"] = "Toyota", ["Modelo"] = "Corolla", ["Anio"] = "2020", ["Activo"] = "true" }, "Modelo", "Yaris");
        await Formulario(navegador, proxy, "Categorias", "api/servicios/categorias", new() { ["Nombre"] = "Categoría Razor", ["Activo"] = "true" }, "Nombre", "Categoría actualizada");
        await Formulario(navegador, proxy, "Servicios", "api/servicios/servicios", new() { ["CategoriaServicioId"] = categoria.GetProperty("id").GetString()!, ["Nombre"] = "Servicio Razor", ["PrecioBase"] = "123.45", ["DuracionEstimada"] = "30", ["Activo"] = "true" }, "PrecioBase", "222.25");
        await Formulario(navegador, proxy, "Mecanicos", "api/mecanicos/mecanicos", new() { ["Nombres"] = "Ana", ["Apellidos"] = "Pérez", ["Documento"] = "RAZOR456", ["Especialidad"] = "Electricidad", ["Activo"] = "true" }, "Apellidos", "López");
        Assert.Equal(HttpStatusCode.OK, (await proxy.DeleteAsync("/api/clientes-vehiculos/clientes/" + propietario.GetProperty("id").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await proxy.DeleteAsync("/api/servicios/categorias/" + categoria.GetProperty("id").GetString())).StatusCode);
    }
    [Theory] [InlineData("Clientes")] [InlineData("Vehiculos")] [InlineData("Categorias")] [InlineData("Servicios")] [InlineData("Mecanicos")]
    public async Task PaginasSinSesionRedirigenAlInicioDeSesion(string modulo)
    {
        const string autorizacion = "http://127.0.0.1:8180/realms/autotaller/protocol/openid-connect/auth";
        using var fabrica = new WebApplicationFactory<FrontendAssembly>().WithWebHostBuilder(builder => builder.ConfigureServices(sp =>
            sp.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o => o.Configuration = new OpenIdConnectConfiguration { AuthorizationEndpoint = autorizacion })));
        using var navegador = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var pagina in new[] { "Index", "Crear" })
        {
            var respuesta = await navegador.GetAsync($"/{modulo}/{pagina}");
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.StartsWith(autorizacion, respuesta.Headers.Location!.ToString());
        }
    }
    [Theory] [InlineData("es-BO")] [InlineData("es-ES")] [InlineData("en-US")]
    public void LimitesDePrecioNoDependenDeLaCultura(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var input = new ServicioInput { CategoriaServicioId = Guid.NewGuid().ToString(), Nombre = "Lavado", PrecioBase = 123.45m };
            Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input, new(input), resultados, true));
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }
    private static async Task Formulario(HttpClient navegador, HttpClient proxy, string modulo, string ruta, Dictionary<string, string> datos, string campo, string valor)
    {
        async Task<HttpResponseMessage> Enviar(string pagina, Dictionary<string, string> campos)
        {
            var html = await navegador.GetStringAsync(pagina);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            var formulario = campos.ToDictionary(x => "Input." + x.Key, x => x.Value);
            formulario["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
            return await navegador.PostAsync(pagina, new FormUrlEncodedContent(formulario));
        }
        var creado = await Enviar($"/{modulo}/Crear", datos);
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        var listado = (await proxy.GetFromJsonAsync<JsonElement[]>(ruta))!;
        var identificador = datos.ContainsKey("Placa") ? "Placa" : datos.ContainsKey("Documento") ? "Documento" : "Nombre";
        var registro = listado.Single(x => x.GetProperty(char.ToLowerInvariant(identificador[0]) + identificador[1..]).ToString() == datos[identificador]);
        var id = registro.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await navegador.GetAsync($"/{modulo}/Index?Ordenar=fecha&Descendente=true")).StatusCode);
        datos[campo] = valor;
        Assert.Equal(HttpStatusCode.Redirect, (await Enviar($"/{modulo}/Editar/{id}", datos)).StatusCode);
        var actualizado = await proxy.GetFromJsonAsync<JsonElement>($"{ruta}/{id}");
        Assert.Equal(valor, actualizado.GetProperty(char.ToLowerInvariant(campo[0]) + campo[1..]).ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await Enviar($"/{modulo}/Eliminar/{id}", new())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await proxy.GetAsync($"{ruta}/{id}")).StatusCode);
    }
    private static string Raiz()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "AutoTallerPro.sln"))) directorio = directorio.Parent;
        return directorio?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
