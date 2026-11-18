using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Infrastructure.Keycloak;

namespace AutoTallerPro.Usuarios.Tests.Identidad;

public sealed class KeycloakIdentidadesClienteTests
{
    private const string Id = "74e31e63-65bf-4d96-a180-3b9a1c5fa201";
    private const string Grupo = "25a97bee-786b-4804-b9f4-7f983ac21778";
    private const string RolId = "0782e901-884f-4903-811f-dd712641db38";
    private const string RutaUsuario = "/admin/realms/autotaller/users/" + Id;
    private const string RutaToken = "/realms/autotaller/protocol/openid-connect/token";

    [Fact]
    public async Task Token_UsaClientCredentialsYSeRenuevaAntesDeExpirar()
    {
        using var prueba = new ClienteDePrueba(peticion => Json(Usuario()));
        await prueba.Cliente.ObtenerAsync(Id);
        prueba.Reloj.Ahora = prueba.Reloj.Ahora.AddSeconds(200);
        await prueba.Cliente.ObtenerAsync(Id);
        prueba.Reloj.Ahora = prueba.Reloj.Ahora.AddSeconds(75);
        await prueba.Cliente.ObtenerAsync(Id);

        var tokens = prueba.Peticiones.Where(p => p.Ruta == RutaToken).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, solicitud =>
        {
            Assert.Equal(HttpMethod.Post, solicitud.Metodo);
            Assert.Contains("grant_type=client_credentials", solicitud.Cuerpo);
            Assert.Contains("client_id=autotaller-usuarios-api", solicitud.Cuerpo);
            Assert.Contains("client_secret=", solicitud.Cuerpo);
            Assert.Null(solicitud.Autorizacion);
        });
        Assert.Equal(new[] { "Bearer tecnico-1", "Bearer tecnico-1", "Bearer tecnico-2" },
            prueba.Admin.Select(x => x.Autorizacion));
    }

    [Fact]
    public async Task Token_RechazoDelEndpointNoLlegaAAdminApi()
    {
        using var prueba = new ClienteDePrueba(
            _ => throw new InvalidOperationException(), tokenEstado: HttpStatusCode.Unauthorized);
        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() => prueba.Cliente.CerrarSesionesAsync(Id));

        Assert.Equal(ErrorIdentidad.NoAutorizado, error.Error);
        Assert.Equal(401, error.EstadoRemoto);
        Assert.False(error.ResultadoIncierto);
        Assert.Empty(prueba.Admin);
    }

    [Fact]
    public async Task Un401InvalidaElTokenSinRepetirLaEscritura()
    {
        int primera = 0;
        using var prueba = new ClienteDePrueba(_ => new HttpResponseMessage(
            primera++ == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent));

        await Assert.ThrowsAsync<OperacionIdentidadException>(() => prueba.Cliente.CerrarSesionesAsync(Id));
        Assert.Single(prueba.Admin);
        await prueba.Cliente.CerrarSesionesAsync(Id);

        Assert.Equal(2, prueba.Peticiones.Count(p => p.Ruta == RutaToken));
        Assert.Equal(new[] { "Bearer tecnico-1", "Bearer tecnico-2" }, prueba.Admin.Select(p => p.Autorizacion));
    }

    [Fact]
    public async Task Consultas_IdentidadExactaDisponibleY404()
    {
        using var prueba = new ClienteDePrueba(p => p.Ruta switch
        {
            RutaUsuario => Json(Usuario()),
            var ruta when ruta.StartsWith("/admin/realms/autotaller/users/", StringComparison.Ordinal) =>
                new HttpResponseMessage(HttpStatusCode.NotFound),
            _ when p.Consulta.Contains("username=libre", StringComparison.Ordinal) => Json(Array.Empty<object>()),
            _ => Json(new[] { Usuario() })
        });

        var cuenta = await prueba.Cliente.ObtenerAsync(Id);
        Assert.Equal(new CuentaKeycloak(Id, "ana.perez", "ana+alta@ejemplo.invalid", "Ana", "Pérez",
            true, false, cuenta!.AccionesRequeridas), cuenta);
        Assert.Equal(new[] { "VERIFY_EMAIL", "UPDATE_PASSWORD" }, cuenta.AccionesRequeridas);
        Assert.Equal(Id, (await prueba.Cliente.BuscarPorUsernameAsync("ana.perez"))!.Id);
        Assert.Equal(Id, (await prueba.Cliente.BuscarPorCorreoAsync("ana+alta@ejemplo.invalid"))!.Id);
        Assert.False(await prueba.Cliente.ExisteUsernameAsync("libre"));
        Assert.Null(await prueba.Cliente.ObtenerAsync("bb030329-7138-427a-a3f3-c2ee95104637"));
        Assert.Contains(prueba.Admin, p => p.Consulta == "?username=ana.perez&exact=true&max=2&briefRepresentation=false");
        Assert.Contains(prueba.Admin, p => p.Consulta == "?email=ana%2Balta%40ejemplo.invalid&exact=true&max=2&briefRepresentation=false");
    }

    [Fact]
    public async Task Crear_NoIncluyeCredencialesYLeeElIdentificadorDeLocation()
    {
        using var prueba = new ClienteDePrueba(_ =>
        {
            var respuesta = new HttpResponseMessage(HttpStatusCode.Created);
            respuesta.Headers.Location = new Uri("http://127.0.0.1:8180" + RutaUsuario);
            return respuesta;
        });
        var id = await prueba.Cliente.CrearAsync(new("ana.perez", "ana@ejemplo.invalid", "Ana", "Pérez"));

        Assert.Equal(Id, id);
        var solicitud = Assert.Single(prueba.Admin);
        Assert.Equal(HttpMethod.Post, solicitud.Metodo);
        Assert.Equal("/admin/realms/autotaller/users", solicitud.Ruta);
        using var json = JsonDocument.Parse(solicitud.Cuerpo!);
        Assert.False(json.RootElement.GetProperty("emailVerified").GetBoolean());
        Assert.True(json.RootElement.GetProperty("enabled").GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("credentials", out _));
        Assert.Equal("/usuarios-administrados", json.RootElement.GetProperty("groups")[0].GetString());
    }

    [Theory]
    [InlineData(409, ErrorIdentidad.Conflicto, false)]
    [InlineData(201, ErrorIdentidad.RespuestaInvalida, true)]
    public async Task Crear_DistingueDuplicadoDeAltaSinIdentificador(int estado, ErrorIdentidad tipo, bool incierto)
    {
        using var prueba = new ClienteDePrueba(_ => new HttpResponseMessage((HttpStatusCode)estado));
        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() =>
            prueba.Cliente.CrearAsync(new("ana.perez", "ana@ejemplo.invalid")));

        Assert.Equal(tipo, error.Error);
        Assert.Equal(incierto, error.ResultadoIncierto);
        Assert.Single(prueba.Admin);
    }

    [Fact]
    public async Task Escritos_ActualizaSoloCamposNecesariosYEnviaLasDosAcciones()
    {
        using var prueba = new ClienteDePrueba(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        await prueba.Cliente.ActualizarDatosAsync(Id, new());
        await prueba.Cliente.ActualizarDatosAsync(Id, new("nuevo@ejemplo.invalid", Nombre: "Ana"));
        await prueba.Cliente.CambiarHabilitacionAsync(Id, false);
        await prueba.Cliente.CambiarHabilitacionAsync(Id, true);
        await prueba.Cliente.CerrarSesionesAsync(Id);
        await prueba.Cliente.SolicitarInvitacionAsync(Id);

        Assert.Collection(prueba.Admin,
            p =>
            {
                Assert.Equal(RutaUsuario, p.Ruta);
                using var datos = JsonDocument.Parse(p.Cuerpo!);
                Assert.Equal(3, datos.RootElement.EnumerateObject().Count());
                Assert.Equal("nuevo@ejemplo.invalid", datos.RootElement.GetProperty("email").GetString());
                Assert.False(datos.RootElement.GetProperty("emailVerified").GetBoolean());
                Assert.Equal("Ana", datos.RootElement.GetProperty("firstName").GetString());
            },
            p => Assert.Equal("""{"enabled":false}""", p.Cuerpo),
            p => Assert.Equal("""{"enabled":true}""", p.Cuerpo),
            p => { Assert.Equal(HttpMethod.Post, p.Metodo); Assert.Equal(RutaUsuario + "/logout", p.Ruta); },
            p =>
            {
                Assert.Equal(HttpMethod.Put, p.Metodo);
                Assert.Equal(RutaUsuario + "/execute-actions-email", p.Ruta);
                Assert.Equal("""["VERIFY_EMAIL","UPDATE_PASSWORD"]""", p.Cuerpo);
            });
    }

    [Theory]
    [InlineData(Roles.Administrador, "usuarios-rol-administrador")]
    [InlineData(Roles.Recepcionista, "usuarios-rol-recepcionista")]
    public async Task Roles_VerificaMapeoYModificaSoloLaPertenencia(string rol, string grupo)
    {
        using var prueba = new ClienteDePrueba(p =>
        {
            if (p.Ruta.EndsWith("/realm/composite", StringComparison.Ordinal))
                return Json(new[] { RepresentacionRol(rol), RepresentacionRol("offline_access") });
            if (p.Ruta.EndsWith("/" + grupo, StringComparison.Ordinal))
                return Json(new { id = Grupo, path = "/" + grupo });
            if (p.Ruta.EndsWith("/role-mappings", StringComparison.Ordinal))
                return Json(new { realmMappings = new[] { RepresentacionRol(rol) } });
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        Assert.Equal(rol, Assert.Single(await prueba.Cliente.ObtenerRolesAsync(Id)));
        await prueba.Cliente.AsignarRolAsync(Id, rol);
        await prueba.Cliente.RetirarRolAsync(Id, rol);

        Assert.Equal(new[] { HttpMethod.Put, HttpMethod.Delete },
            prueba.Admin.Where(p => p.Ruta == RutaUsuario + "/groups/" + Grupo).Select(p => p.Metodo));
    }

    [Fact]
    public async Task Roles_RechazaRolesNoPermitidosSinHacerPeticiones()
    {
        using var prueba = new ClienteDePrueba(_ => throw new InvalidOperationException());

        await Assert.ThrowsAsync<ArgumentException>(() => prueba.Cliente.AsignarRolAsync(Id, "realm-admin"));
        await Assert.ThrowsAsync<ArgumentException>(() => prueba.Cliente.RetirarRolAsync(Id, "administrador"));
        Assert.Empty(prueba.Peticiones);
    }

    [Theory]
    [InlineData("otro", RolId, false)]
    [InlineData(Roles.Recepcionista, "id-invalido", false)]
    [InlineData(Roles.Recepcionista, RolId, true)]
    public async Task Roles_RechazaMapeoInconsistenteSinEscribir(string rol, string id, bool clientRole)
    {
        using var prueba = new ClienteDePrueba(p => p.Ruta.EndsWith("/role-mappings", StringComparison.Ordinal)
            ? Json(new { realmMappings = new[] { new { name = rol, id, clientRole } } })
            : Json(new { id = Grupo, path = "/usuarios-rol-recepcionista" }));

        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() =>
            prueba.Cliente.AsignarRolAsync(Id, Roles.Recepcionista));

        Assert.Equal(ErrorIdentidad.RespuestaInvalida, error.Error);
        Assert.All(prueba.Admin, p => Assert.Equal(HttpMethod.Get, p.Metodo));
    }

    [Theory]
    [InlineData(401, ErrorIdentidad.NoAutorizado, false)]
    [InlineData(403, ErrorIdentidad.Prohibido, false)]
    [InlineData(404, ErrorIdentidad.NoEncontrado, false)]
    [InlineData(409, ErrorIdentidad.Conflicto, false)]
    [InlineData(500, ErrorIdentidad.NoDisponible, true)]
    [InlineData(503, ErrorIdentidad.NoDisponible, true)]
    [InlineData(504, ErrorIdentidad.TiempoAgotado, true)]
    public async Task Escritos_ClasificaFallosRemotosSinReenviar(int estado, ErrorIdentidad tipo, bool incierto)
    {
        using var prueba = new ClienteDePrueba(
            _ => new HttpResponseMessage((HttpStatusCode)estado) { Content = JsonContent.Create("datos-internos") });
        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() => prueba.Cliente.SolicitarInvitacionAsync(Id));

        Assert.Equal(tipo, error.Error);
        Assert.Equal(estado, error.EstadoRemoto);
        Assert.Equal(incierto, error.ResultadoIncierto);
        Assert.DoesNotContain("datos-internos", error.ToString());
        Assert.Single(prueba.Admin);
    }

    [Fact]
    public async Task TimeoutAlEnviar_MarcaResultadoInciertoYNoReenvia()
    {
        using var prueba = new ClienteDePrueba(_ => throw new TaskCanceledException());
        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() => prueba.Cliente.SolicitarInvitacionAsync(Id));

        Assert.Equal(ErrorIdentidad.TiempoAgotado, error.Error);
        Assert.Equal(nameof(IIdentidadesKeycloak.SolicitarInvitacionAsync), error.Operacion);
        Assert.True(error.ResultadoIncierto);
        Assert.Single(prueba.Admin);
    }

    [Fact]
    public async Task ConfiguracionIncompleta_NoSolicitaToken()
    {
        using var prueba = new ClienteDePrueba(_ => throw new InvalidOperationException(), secreto: null);
        var error = await Assert.ThrowsAsync<OperacionIdentidadException>(() => prueba.Cliente.ObtenerAsync(Id));

        Assert.Equal(ErrorIdentidad.Configuracion, error.Error);
        Assert.Empty(prueba.Peticiones);
    }

    private static object Usuario() => new
    {
        id = Id, username = "ana.perez", email = "ana+alta@ejemplo.invalid", firstName = "Ana", lastName = "Pérez",
        enabled = true, emailVerified = false, requiredActions = new[] { "VERIFY_EMAIL", "UPDATE_PASSWORD" }
    };

    private static object RepresentacionRol(string nombre) => new { id = RolId, name = nombre, clientRole = false };

    private static HttpResponseMessage Json(object objeto) => new(HttpStatusCode.OK) { Content = JsonContent.Create(objeto) };

    private sealed record Peticion(HttpMethod Metodo, string Ruta, string Consulta, string? Cuerpo, string? Autorizacion);

    private sealed class ClienteDePrueba : IDisposable
    {
        private readonly HttpClient _http;
        private readonly TokenTecnicoKeycloak _tokens;
        public RelojDePrueba Reloj { get; } = new();
        public List<Peticion> Peticiones { get; } = [];
        public IEnumerable<Peticion> Admin => Peticiones.Where(p => p.Ruta != RutaToken);
        public KeycloakIdentidadesCliente Cliente { get; }

        public ClienteDePrueba(Func<Peticion, HttpResponseMessage> responder, string? secreto = "secreto-pruebas",
            HttpStatusCode tokenEstado = HttpStatusCode.OK)
        {
            int tokensEmitidos = 0;
            var opciones = new OpcionesKeycloakAdmin
            {
                BaseUrl = "http://keycloak:8080", Realm = "autotaller", ClientId = "autotaller-usuarios-api",
                ClientSecret = secreto, PermitirHttpLocal = true
            };
            _http = new HttpClient(new TransporteDePrueba(p =>
            {
                Peticiones.Add(p);
                if (p.Ruta != RutaToken)
                    return responder(p);
                return tokenEstado == HttpStatusCode.OK
                    ? Json(new { access_token = $"tecnico-{++tokensEmitidos}", expires_in = 300 })
                    : new HttpResponseMessage(tokenEstado);
            }));
            _tokens = new TokenTecnicoKeycloak(opciones, Reloj);
            Cliente = new KeycloakIdentidadesCliente(_http, opciones, _tokens);
        }

        public void Dispose()
        {
            _http.Dispose();
            _tokens.Dispose();
        }
    }

    private sealed class TransporteDePrueba(Func<Peticion, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken cancellationToken) =>
            responder(new Peticion(solicitud.Method, solicitud.RequestUri!.AbsolutePath, solicitud.RequestUri.Query,
                solicitud.Content is null ? null : await solicitud.Content.ReadAsStringAsync(cancellationToken),
                solicitud.Headers.Authorization?.ToString()));
    }

    private sealed class RelojDePrueba : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
