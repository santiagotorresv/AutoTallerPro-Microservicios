using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AutoTallerPro.Usuarios.Tests;

public sealed class UsuariosApiJwtTests(UsuariosApiFactory api) : IClassFixture<UsuariosApiFactory>
{
    [Theory]
    [InlineData("DELETE", "/usuarios/1")]
    [InlineData("POST", "/usuarios/1/contrasena")]
    [InlineData("POST", "/aprovisionar-primer-administrador")]
    public async Task Api_NoExponeOperacionesReservadas(string metodo, string ruta)
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = Bearer(api.EmitirToken([Roles.Administrador]));
        using var respuesta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));
        Assert.Contains(respuesta.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task Salud_AdmiteAccesoAnonimo()
    {
        using var cliente = api.CreateClient();
        using var respuesta = await cliente.GetAsync("/salud");
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("OK", cuerpo.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Yo_SinToken_RechazaInclusoConCabecerasDeIdentidad()
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Usuario-Id", "1");
        cliente.DefaultRequestHeaders.Add("X-Usuario-Nombre", "actor-falso");
        cliente.DefaultRequestHeaders.Add("X-Usuario-Rol", Roles.Administrador);

        using var respuesta = await cliente.GetAsync("/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains(respuesta.Headers.WwwAuthenticate, valor => valor.Scheme == "Bearer");
    }

    [Theory]
    [InlineData(Roles.Administrador)]
    [InlineData(Roles.Recepcionista)]
    public async Task Yo_DevuelveSoloIdentidadPropiaYRolDeNegocio(string rol)
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = Bearer(api.EmitirToken([rol, "offline_access"]));

        using var respuesta = await cliente.GetAsync("/yo");
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var identidad = cuerpo.RootElement;

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(3, identidad.EnumerateObject().Count());
        Assert.Equal(UsuariosApiFactory.Sujeto, identidad.GetProperty("id").GetString());
        Assert.Equal(UsuariosApiFactory.Username, identidad.GetProperty("username").GetString());
        Assert.Equal(rol, Assert.Single(identidad.GetProperty("roles").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Yo_SinRolDeRealmAdmitido_Devuelve403()
    {
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = Bearer(api.EmitirToken(["offline_access", "administrador"]));

        using var respuesta = await cliente.GetAsync("/yo");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("malformado")]
    [InlineData("firma")]
    [InlineData("expirado")]
    [InlineData("audiencia")]
    [InlineData("emisor")]
    [InlineData("id_token")]
    [InlineData("sin_sujeto")]
    [InlineData("sin_username")]
    public async Task Yo_RechazaTokensNoValidos(string caso)
    {
        string token = caso switch
        {
            "malformado" => "jwt-invalido",
            "firma" => AlterarFirma(api.EmitirToken([Roles.Administrador])),
            "expirado" => api.EmitirToken([Roles.Administrador], expirado: true),
            "audiencia" => api.EmitirToken([Roles.Administrador], audiencia: "otra-api"),
            "emisor" => api.EmitirToken([Roles.Administrador], emisor: UsuariosApiFactory.Emisor + "/"),
            "id_token" => api.EmitirToken([Roles.Administrador], tipo: "ID"),
            "sin_sujeto" => api.EmitirToken([Roles.Administrador], sujeto: ""),
            "sin_username" => api.EmitirToken([Roles.Administrador], username: ""),
            _ => throw new ArgumentOutOfRangeException(nameof(caso))
        };
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = Bearer(token);

        using var respuesta = await cliente.GetAsync("/yo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Administrador, true)]
    [InlineData(Roles.Recepcionista, false)]
    public async Task SoloAdministrador_EvaluaAlUsuarioAutenticado(string rol, bool esperado)
    {
        using var alcance = api.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var contexto = new DefaultHttpContext { RequestServices = servicios };
        contexto.Request.Headers.Authorization = "Bearer " + api.EmitirToken([rol]);
        var autenticacion = await contexto.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(autenticacion.Succeeded);

        var autorizacion = servicios.GetRequiredService<IAuthorizationService>();
        var resultado = await autorizacion.AuthorizeAsync(autenticacion.Principal!, "SoloAdministrador");

        Assert.Equal(esperado, resultado.Succeeded);
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static string AlterarFirma(string token)
    {
        var partes = token.Split('.');
        byte[] firma = Base64UrlEncoder.DecodeBytes(partes[2]);
        firma[0] ^= 1;
        partes[2] = Base64UrlEncoder.Encode(firma);
        return string.Join('.', partes);
    }
}

public sealed class UsuariosApiFactory : WebApplicationFactory<Program>
{
    internal const string Emisor = "http://127.0.0.1:8180/realms/autotaller";
    private const string Audiencia = "autotaller-api";
    internal const string Sujeto = "e8d4b52a-4f49-47be-91c0-7ad32eec005c";
    internal const string Username = "usuario.prueba";

    private readonly RsaSecurityKey _claveParaEscritura;
    private readonly RsaSecurityKey _clavePublica;

    public UsuariosApiFactory()
    {
        using var rsa = RSA.Create(2048);
        _claveParaEscritura = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = "prueba" };
        _clavePublica = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = "prueba" };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Authority", Emisor);
        builder.UseSetting("Authentication:Audience", Audiencia);
        builder.ConfigureTestServices(servicios =>
            servicios.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opciones =>
            {
                opciones.Configuration = new OpenIdConnectConfiguration { Issuer = Emisor };
                opciones.Configuration.SigningKeys.Add(_clavePublica);
            }));
    }

    internal string EmitirToken(
        string[] roles, string audiencia = Audiencia, string emisor = Emisor, bool expirado = false,
        string tipo = "Bearer", string sujeto = Sujeto, string username = Username)
    {
        var ahora = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = emisor,
            Audience = audiencia,
            IssuedAt = ahora.AddMinutes(-2),
            NotBefore = ahora.AddMinutes(-2),
            Expires = ahora.AddMinutes(expirado ? -1 : 2),
            SigningCredentials = new SigningCredentials(_claveParaEscritura, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sujeto,
                ["preferred_username"] = username,
                ["email"] = "usuario@ejemplo.invalid",
                ["typ"] = tipo,
                ["realm_access"] = JsonSerializer.SerializeToElement(new { roles }),
                [ClaimTypes.Role] = Roles.Administrador
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
