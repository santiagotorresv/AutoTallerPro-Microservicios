using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AutoTallerPro.Frontend.ApiUsuarios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTallerPro.Frontend.Tests;

public sealed class ClienteUsuariosTests
{
    private static readonly Guid Clave = Guid.Parse("829e5ab6-14c8-4c88-9271-b76e6895d4e2");

    [Fact]
    public async Task AltaEnviaTokenClaveYSoloLosCuatroCampos()
    {
        var api = new ApiFalsa(HttpStatusCode.Created, """{"id":42,"username":"ana.perez","estadoInvitacion":"correoSolicitado"}""");

        var respuesta = await Cliente(api).CrearAsync("ana@ejemplo.invalid", "Recepcionista", "Ana", "Pérez", Clave);

        Assert.True(respuesta.Exito);
        Assert.Equal("ana.perez", respuesta.Valor!.Username);
        Assert.Equal(HttpMethod.Post, api.Metodo);
        Assert.Equal("/api/usuarios/usuarios", api.Ruta);
        Assert.Equal("Bearer token-de-prueba", api.Autorizacion);
        Assert.Equal(Clave.ToString("D"), api.ClaveIdempotencia);
        var cuerpo = JsonDocument.Parse(api.Cuerpo!).RootElement;
        Assert.Equal(["email", "rol", "nombre", "apellido"], cuerpo.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task CorreoDuplicadoMuestraElConflictoYPermiteOtraClave()
    {
        var api = ApiFalsa.Problema(HttpStatusCode.Conflict, "correo_duplicado");

        var respuesta = await Cliente(api).CrearAsync("ana@ejemplo.invalid", "Recepcionista", "Ana", "Pérez", Clave);

        Assert.False(respuesta.Exito);
        Assert.Equal("correo_duplicado", respuesta.Codigo);
        Assert.Contains("correo ya está asociado", respuesta.Mensaje);
        Assert.False(respuesta.ConservarClave);
    }

    [Fact]
    public async Task ReenvioDentroDelIntervaloRespetaRetryAfter()
    {
        var api = ApiFalsa.Problema((HttpStatusCode)429, "intervalo_invitacion");
        api.RetryAfter = 90;

        var respuesta = await Cliente(api).ReenviarInvitacionAsync(42, Clave);

        Assert.Equal(429, respuesta.Estado);
        Assert.Equal(90, respuesta.EsperaSegundos);
        Assert.Contains("90 segundos", respuesta.Mensaje);
        Assert.Equal("/api/usuarios/usuarios/42/reenviar-invitacion", api.Ruta);
    }

    [Theory]
    [InlineData("administracion_pendiente")]
    [InlineData("dependencia_no_disponible")]
    [InlineData("envio_incierto")]
    public async Task ResultadoInciertoConservaLaMismaClave(string codigo)
    {
        var respuesta = await Cliente(ApiFalsa.Problema(HttpStatusCode.ServiceUnavailable, codigo)).DesactivarAsync(42, Clave);

        Assert.False(respuesta.Exito);
        Assert.True(respuesta.ConservarClave);
    }

    [Fact]
    public async Task EnvioFallidoNoRepiteAutomaticamente()
    {
        var respuesta = await Cliente(ApiFalsa.Problema(HttpStatusCode.ServiceUnavailable, "envio_fallido")).ReenviarInvitacionAsync(42, Clave);

        Assert.False(respuesta.ConservarClave);
    }

    [Fact]
    public async Task SinRespuestaDelGatewayElResultadoEsIncierto()
    {
        var api = new ApiFalsa(HttpStatusCode.OK, "") { Error = new HttpRequestException("sin conexión") };

        var respuesta = await Cliente(api).EditarAsync(42, "ana@ejemplo.invalid", "Recepcionista", true, Clave);

        Assert.Equal(0, respuesta.Estado);
        Assert.True(respuesta.ConservarClave);
    }

    [Fact]
    public async Task ForbiddenSinCuerpoSeInterpretaPorEstado()
    {
        var respuesta = await Cliente(new ApiFalsa(HttpStatusCode.Forbidden, "")).ResumenAsync();

        Assert.Equal(403, respuesta.Estado);
        Assert.Equal("Su cuenta no tiene permiso para realizar esta operación.", respuesta.Mensaje);
    }

    [Fact]
    public async Task SinSesionNoLlamaAlGateway()
    {
        var api = new ApiFalsa(HttpStatusCode.OK, "{}");

        var respuesta = await Cliente(api, token: null).ResumenAsync();

        Assert.Equal(401, respuesta.Estado);
        Assert.Null(api.Ruta);
    }

    [Fact]
    public async Task ListadoEnviaLosFiltros()
    {
        var api = new ApiFalsa(HttpStatusCode.OK, """{"usuarios":[],"total":0,"pagina":2,"tamanoPagina":10}""");

        var respuesta = await Cliente(api).ListarAsync(" ana ", "Recepcionista", false, 2, 10);

        Assert.True(respuesta.Exito);
        Assert.Equal("/api/usuarios/usuarios?busqueda=ana&rol=Recepcionista&activo=false&pagina=2&tamanoPagina=10", api.Ruta);
    }

    private static ClienteUsuarios Cliente(ApiFalsa api, string? token = "token-de-prueba")
    {
        var servicios = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new SesionFalsa(token))
            .BuildServiceProvider();
        var accesor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = servicios } };
        return new ClienteUsuarios(new HttpClient(api) { BaseAddress = new Uri("http://gateway.prueba/") }, accesor);
    }

    private sealed class ApiFalsa(HttpStatusCode estado, string cuerpo, string tipo = "application/json") : HttpMessageHandler
    {
        public HttpMethod? Metodo { get; private set; }
        public string? Ruta { get; private set; }
        public string? Autorizacion { get; private set; }
        public string? ClaveIdempotencia { get; private set; }
        public string? Cuerpo { get; private set; }
        public int? RetryAfter { get; set; }
        public Exception? Error { get; init; }

        public static ApiFalsa Problema(HttpStatusCode estado, string codigo) =>
            new(estado, $$"""{"title":"Problema","status":{{(int)estado}},"code":"{{codigo}}"}""", "application/problem+json");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            Metodo = solicitud.Method;
            Ruta = solicitud.RequestUri!.PathAndQuery;
            Autorizacion = solicitud.Headers.Authorization?.ToString();
            ClaveIdempotencia = solicitud.Headers.TryGetValues("Idempotency-Key", out var claves) ? claves.Single() : null;
            Cuerpo = solicitud.Content is null ? null : await solicitud.Content.ReadAsStringAsync(cancelacion);
            if (Error is not null)
            {
                throw Error;
            }

            var respuesta = new HttpResponseMessage(estado);
            if (cuerpo.Length > 0)
            {
                respuesta.Content = new StringContent(cuerpo, Encoding.UTF8, tipo);
            }

            if (RetryAfter is { } segundos)
            {
                respuesta.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(segundos));
            }

            return respuesta;
        }
    }

    private sealed class SesionFalsa(string? token) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (token is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var propiedades = new AuthenticationProperties();
            propiedades.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("Prueba")), propiedades, "Prueba");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
