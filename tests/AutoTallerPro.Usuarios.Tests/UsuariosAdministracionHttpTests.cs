using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Tests.Persistencia;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoTallerPro.Usuarios.Tests;

public sealed class UsuariosAdministracionHttpTests(UsuariosApiFactory api) : IClassFixture<UsuariosApiFactory>
{
    [Theory]
    [InlineData("POST", "/usuarios")]
    [InlineData("GET", "/usuarios")]
    [InlineData("GET", "/usuarios/1")]
    [InlineData("GET", "/usuarios/resumen")]
    public async Task Endpoints_ExigenTokenYAdministrador(string metodo, string ruta)
    {
        using var http = api.CreateClient();
        using var anonimo = await http.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

        http.DefaultRequestHeaders.Authorization = new("Bearer", api.EmitirToken([Roles.Recepcionista]));
        using var recepcionista = await http.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));
        Assert.Equal(HttpStatusCode.Forbidden, recepcionista.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Alta_RechazaTokenAnteriorALaDesactivacionODegradacion(bool desactivar)
    {
        var identidades = new KeycloakPrueba(Actor);
        if (desactivar)
        {
            await identidades.CambiarHabilitacionAsync(Actor.Sujeto, false);
        }
        else
        {
            await identidades.AsignarRolAsync(Actor.Sujeto, Roles.Recepcionista);
        }

        using var personalizada = Personalizar(identidades);
        using var http = ComoAdministrador(personalizada.CreateClient());
        using var respuesta = await http.PostAsJsonAsync("/usuarios", Datos());
        await ComprobarAsync(respuesta, HttpStatusCode.Forbidden, "administrador_no_vigente");
        Assert.Equal(0, identidades.Altas);
    }

    [Fact]
    public async Task Alta_RechazaContrasenaYValidaRolYClave()
    {
        using var personalizada = Personalizar(new KeycloakPrueba(Actor));
        using var http = ComoAdministrador(personalizada.CreateClient());
        using var conContrasena = await http.PostAsJsonAsync("/usuarios",
            new { email = "prueba@ejemplo.invalid", rol = Roles.Administrador, password = "un-campo-no-admitido" });
        Assert.Equal(HttpStatusCode.BadRequest, conContrasena.StatusCode);
        using var rol = await http.PostAsJsonAsync("/usuarios", Datos() with { Rol = "realm-admin" });
        await ComprobarAsync(rol, HttpStatusCode.BadRequest, "datos_invalidos");

        http.DefaultRequestHeaders.Remove("Idempotency-Key");
        using var sinClave = await http.PostAsJsonAsync("/usuarios", Datos());
        await ComprobarAsync(sinClave, HttpStatusCode.BadRequest, "clave_invalida");
    }

    [Fact]
    public async Task ErroresDeNegocioYDependencia_SeTraducenSinDetallesInternos()
    {
        var identidades = new KeycloakPrueba(Actor);
        using var personalizada = Personalizar(identidades, new AltasDuplicadas());
        using var http = ComoAdministrador(personalizada.CreateClient());
        using var duplicado = await http.PostAsJsonAsync("/usuarios", Datos());
        await ComprobarAsync(duplicado, HttpStatusCode.Conflict, "correo_duplicado");

        using var detalle = await http.GetAsync("/usuarios/7");
        await ComprobarAsync(detalle, HttpStatusCode.NotFound, "usuario_no_encontrado");
        using var filtros = await http.GetAsync("/usuarios?pagina=0&tamanoPagina=101");
        await ComprobarAsync(filtros, HttpStatusCode.BadRequest, "datos_invalidos");

        using var dependencia = Personalizar(identidades, new AltasNoDisponibles());
        using var cliente = ComoAdministrador(dependencia.CreateClient());
        using var temporal = await cliente.PostAsJsonAsync("/usuarios", Datos());
        await ComprobarAsync(temporal, HttpStatusCode.ServiceUnavailable, "dependencia_no_disponible");
        Assert.DoesNotContain("detalle-reservado", await temporal.Content.ReadAsStringAsync());
    }

    private static readonly IdentidadKeycloak Actor = new(UsuariosApiFactory.Emisor, UsuariosApiFactory.Sujeto);
    private static SolicitudAltaUsuario Datos() => new("usuario@ejemplo.invalid", Roles.Recepcionista, "Ana", "Pérez");

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Personalizar(
        IIdentidadesKeycloak identidades, IAltasUsuarios? altas = null) =>
        api.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<IIdentidadesKeycloak>();
            servicios.RemoveAll<IAltasUsuarios>();
            servicios.RemoveAll<IConsultaFichasUsuarios>();
            servicios.AddSingleton(identidades);
            servicios.AddSingleton(altas ?? new AltasDuplicadas());
            servicios.AddSingleton<IConsultaFichasUsuarios, SinFichas>();
        }));

    private HttpClient ComoAdministrador(HttpClient http)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", api.EmitirToken([Roles.Administrador]));
        http.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return http;
    }

    private static async Task ComprobarAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(codigo, cuerpo.RootElement.GetProperty("code").GetString());
    }

    private sealed class AltasDuplicadas : IAltasUsuarios
    {
        public Task<IAltaUsuario> AbrirAsync(IdentidadKeycloak actor, Guid clave, string correo, byte[] huella,
            CancellationToken cancellationToken)
        {
            Assert.Equal(Actor, actor);
            throw new OperacionUsuariosException(ErrorUsuarios.CorreoDuplicado);
        }
    }

    private sealed class AltasNoDisponibles : IAltasUsuarios
    {
        public Task<IAltaUsuario> AbrirAsync(IdentidadKeycloak actor, Guid clave, string correo, byte[] huella,
            CancellationToken cancellationToken) =>
            throw new OperacionIdentidadException("detalle-reservado", ErrorIdentidad.NoDisponible, 503);
    }

    private sealed class SinFichas : IConsultaFichasUsuarios
    {
        public Task<IReadOnlyList<FichaUsuario>> ListarAsync(string emisor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FichaUsuario>>([]);
        public Task<FichaUsuario?> ObtenerAsync(string emisor, int id, CancellationToken cancellationToken) =>
            Task.FromResult<FichaUsuario?>(null);
    }
}

