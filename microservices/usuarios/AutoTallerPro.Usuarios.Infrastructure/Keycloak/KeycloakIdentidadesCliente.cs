using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Validaciones;
using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Infrastructure.Keycloak;

public sealed class KeycloakIdentidadesCliente(
    HttpClient http, OpcionesKeycloakAdmin opciones, TokenTecnicoKeycloak tokens) : IIdentidadesKeycloak
{
    private const string GrupoGestionado = "/usuarios-administrados";
    private const string AtributoAlta = "p4AltaId";
    private static readonly string[] AccionesInvitacion = ["VERIFY_EMAIL", "UPDATE_PASSWORD"];

    public async Task<CuentaKeycloak?> ObtenerAsync(string id, CancellationToken cancellationToken = default)
    {
        const string operacion = nameof(ObtenerAsync);
        using var respuesta = await EnviarAsync(HttpMethod.Get, RutaUsuario(id), operacion, null, cancellationToken);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        TransporteKeycloak.ExigirEstado(respuesta, HttpStatusCode.OK, operacion, false);
        var usuario = await TransporteKeycloak.LeerAsync<UsuarioRemoto>(respuesta, operacion, cancellationToken);
        return Convertir(usuario, operacion);
    }

    public Task<CuentaKeycloak?> BuscarPorUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return BuscarExactoAsync("username", username, nameof(BuscarPorUsernameAsync), cancellationToken);
    }

    public Task<CuentaKeycloak?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        return BuscarExactoAsync("email", correo, nameof(BuscarPorCorreoAsync), cancellationToken);
    }

    public async Task<bool> ExisteUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        await BuscarPorUsernameAsync(username, cancellationToken) is not null;

    public async Task<string> CrearAsync(NuevaCuentaKeycloak cuenta, CancellationToken cancellationToken = default)
    {
        const string operacion = nameof(CrearAsync);
        ArgumentNullException.ThrowIfNull(cuenta);
        if (!ReglasCuenta.UsuarioValido(cuenta.Username) || !ReglasCuenta.EmailValido(cuenta.Correo))
        {
            throw new ArgumentException("El username y el correo deben ser válidos.", nameof(cuenta));
        }

        using var respuesta = await EnviarAsync(HttpMethod.Post, "users", operacion, new
        {
            username = cuenta.Username,
            email = cuenta.Correo,
            firstName = cuenta.Nombre,
            lastName = cuenta.Apellido,
            enabled = true,
            emailVerified = false,
            groups = new[] { GrupoGestionado },
            requiredActions = AccionesInvitacion,
            attributes = cuenta.CorrelacionAlta is { } correlacion
                ? new Dictionary<string, string[]> { [AtributoAlta] = [correlacion.ToString("D")] }
                : null
        }, cancellationToken);
        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.Conflicto, 409,
                campoEnConflicto: await LeerCampoEnConflictoAsync(respuesta, cancellationToken));
        }

        TransporteKeycloak.ExigirEstado(respuesta, HttpStatusCode.Created, operacion, true);

        var uriSolicitud = DireccionAdmin(opciones.ObtenerBase(operacion), "users");
        var location = respuesta.Headers.Location;
        if (location is not null)
        {
            var destino = location.IsAbsoluteUri ? location : new Uri(uriSolicitud, location);
            string prefijo = uriSolicitud.AbsolutePath + "/";
            if (destino.IsAbsoluteUri && destino.AbsolutePath.StartsWith(prefijo, StringComparison.Ordinal))
            {
                var id = destino.AbsolutePath[prefijo.Length..];
                if (Guid.TryParseExact(id, "D", out _))
                {
                    return id;
                }
            }
        }

        throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida, 201, resultadoIncierto: true);
    }

    public async Task ActualizarDatosAsync(
        string id, DatosCuentaKeycloak datos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        string ruta = RutaUsuario(id);
        var cambios = new Dictionary<string, object>();
        if (datos.NuevoCorreo is { } correo)
        {
            if (!ReglasCuenta.EmailValido(correo))
            {
                throw new ArgumentException("El correo debe ser válido.", nameof(datos));
            }

            cambios.Add("email", correo);
            cambios.Add("emailVerified", false);
        }

        if (datos.Nombre is { } nombre)
        {
            cambios.Add("firstName", nombre);
        }

        if (datos.Apellido is { } apellido)
        {
            cambios.Add("lastName", apellido);
        }

        if (datos.AccionesRequeridas is { } acciones)
        {
            cambios.Add("requiredActions", acciones);
        }

        if (cambios.Count != 0)
        {
            await EscribirAsync(HttpMethod.Put, ruta, nameof(ActualizarDatosAsync), cambios, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> ObtenerRolesAsync(string id, CancellationToken cancellationToken = default)
        => await ObtenerRolesAsync(id, "/composite", nameof(ObtenerRolesAsync), cancellationToken);

    public Task<IReadOnlyList<string>> ObtenerRolesDirectosAsync(string id, CancellationToken cancellationToken = default)
        => ObtenerRolesAsync(id, "", nameof(ObtenerRolesDirectosAsync), cancellationToken);

    private async Task<IReadOnlyList<string>> ObtenerRolesAsync(
        string id, string sufijo, string operacion, CancellationToken cancellationToken)
    {
        var roles = await ObtenerJsonAsync<RolRemoto[]>(RutaUsuario(id) + "/role-mappings/realm" + sufijo,
            operacion, cancellationToken);
        return roles.Where(rol => Roles.EsDeNegocio(rol.Name) && !rol.ClientRole && !string.IsNullOrWhiteSpace(rol.Id))
            .Select(rol => rol.Name!).Distinct(StringComparer.Ordinal).ToArray();
    }

    public Task AsignarRolAsync(string id, string rol, CancellationToken cancellationToken = default) =>
        CambiarRolAsync(HttpMethod.Put, id, rol, nameof(AsignarRolAsync), cancellationToken);

    public Task RetirarRolAsync(string id, string rol, CancellationToken cancellationToken = default) =>
        CambiarRolAsync(HttpMethod.Delete, id, rol, nameof(RetirarRolAsync), cancellationToken);

    public Task CambiarHabilitacionAsync(string id, bool habilitada, CancellationToken cancellationToken = default) =>
        EscribirAsync(HttpMethod.Put, RutaUsuario(id), nameof(CambiarHabilitacionAsync),
            new { enabled = habilitada }, cancellationToken);

    public Task CerrarSesionesAsync(string id, CancellationToken cancellationToken = default) =>
        EscribirAsync(HttpMethod.Post, RutaUsuario(id) + "/logout", nameof(CerrarSesionesAsync),
            null, cancellationToken);

    public Task SolicitarInvitacionAsync(string id, CancellationToken cancellationToken = default) =>
        EscribirAsync(HttpMethod.Put, RutaUsuario(id) + "/execute-actions-email", nameof(SolicitarInvitacionAsync),
            AccionesInvitacion, cancellationToken);

    public Task SolicitarInvitacionAsync(string id, IReadOnlyList<AccionInvitacion> acciones,
        CancellationToken cancellationToken = default)
    {
        if (acciones.Count == 0 || acciones.Any(accion => !Enum.IsDefined(accion)))
        {
            throw new ArgumentException("Indica las acciones de invitación pendientes.", nameof(acciones));
        }
        return EscribirAsync(HttpMethod.Put, RutaUsuario(id) + "/execute-actions-email", nameof(SolicitarInvitacionAsync),
            acciones.Select(accion => accion.ToString()).Distinct().ToArray(), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ObtenerGruposAsync(string id, CancellationToken cancellationToken = default)
    {
        var grupos = await ObtenerJsonAsync<GrupoRemoto[]>(RutaUsuario(id) + "/groups?briefRepresentation=false",
            nameof(ObtenerGruposAsync), cancellationToken);
        if (grupos.Any(grupo => string.IsNullOrWhiteSpace(grupo.Path)))
        {
            throw new OperacionIdentidadException(nameof(ObtenerGruposAsync), ErrorIdentidad.RespuestaInvalida);
        }
        return grupos.Select(grupo => grupo.Path!).ToArray();
    }

    public async Task<IReadOnlyList<CuentaKeycloak>> ListarAdministradasAsync(CancellationToken cancellationToken = default)
    {
        const string operacion = nameof(ListarAdministradasAsync);
        const int tamano = 100;
        var grupo = await ObtenerJsonAsync<GrupoRemoto>("group-by-path/" + GrupoGestionado[1..], operacion, cancellationToken);
        if (grupo.Path != GrupoGestionado || !Guid.TryParseExact(grupo.Id, "D", out _))
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }

        var usuarios = new Dictionary<string, CuentaKeycloak>(StringComparer.Ordinal);
        for (int offset = 0; ; offset = checked(offset + tamano))
        {
            var ruta = FormattableString.Invariant(
                $"groups/{grupo.Id}/members?first={offset}&max={tamano}&briefRepresentation=false");
            var pagina = await ObtenerJsonAsync<UsuarioRemoto[]>(ruta, operacion, cancellationToken);
            foreach (var remoto in pagina)
            {
                var cuenta = Convertir(remoto, operacion);
                if (!usuarios.TryAdd(cuenta.Id, cuenta))
                {
                    throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
                }
            }
            if (pagina.Length < tamano)
            {
                return usuarios.Values.ToArray();
            }
        }
    }

    public async Task<bool> TieneContrasenaAsync(string id, CancellationToken cancellationToken = default)
    {
        var credenciales = await ObtenerJsonAsync<TipoCredencial[]>(RutaUsuario(id) + "/credentials",
            nameof(TieneContrasenaAsync), cancellationToken);
        return credenciales.Any(credencial => credencial.Type == "password");
    }

    public async Task<bool> TieneSesionesAsync(string id, CancellationToken cancellationToken = default)
    {
        var sesiones = await ObtenerJsonAsync<JsonElement[]>(RutaUsuario(id) + "/sessions",
            nameof(TieneSesionesAsync), cancellationToken);
        return sesiones.Length > 0;
    }

    public async Task<string> ObtenerEmisorAsync(CancellationToken cancellationToken = default)
    {
        const string operacion = nameof(ObtenerEmisorAsync);
        var baseUrl = opciones.ObtenerBase(operacion);
        using var solicitud = new HttpRequestMessage(HttpMethod.Get,
            new Uri(baseUrl, $"realms/{opciones.Realm}/.well-known/openid-configuration"));
        using var respuesta = await TransporteKeycloak.EnviarAsync(http, solicitud, operacion, false, cancellationToken);
        TransporteKeycloak.ExigirEstado(respuesta, HttpStatusCode.OK, operacion, false);
        var metadatos = await TransporteKeycloak.LeerAsync<MetadatosOidc>(respuesta, operacion, cancellationToken);
        return !string.IsNullOrWhiteSpace(metadatos.Issuer) ? metadatos.Issuer
            : throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
    }

    private async Task<CuentaKeycloak?> BuscarExactoAsync(
        string campo, string valor, string operacion, CancellationToken cancellationToken)
    {
        var usuarios = await ObtenerJsonAsync<UsuarioRemoto[]>(
            $"users?{campo}={Uri.EscapeDataString(valor)}&exact=true&max=2&briefRepresentation=false",
            operacion, cancellationToken);
        return usuarios.Length switch
        {
            0 => null,
            1 => Convertir(usuarios[0], operacion),
            _ => throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida)
        };
    }

    private static async Task<CampoIdentidad?> LeerCampoEnConflictoAsync(
        HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            var conflicto = await respuesta.Content.ReadFromJsonAsync<ConflictoRemoto>(cancellationToken);
            return conflicto?.ErrorMessage switch
            {
                "User exists with same username" => CampoIdentidad.Username,
                "User exists with same email" => CampoIdentidad.Correo,
                _ => null
            };
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task CambiarRolAsync(
        HttpMethod metodo, string id, string rol, string operacion, CancellationToken cancellationToken)
    {
        string grupoNombre = rol switch
        {
            Roles.Administrador => "usuarios-rol-administrador",
            Roles.Recepcionista => "usuarios-rol-recepcionista",
            _ => throw new ArgumentException("El rol debe ser un rol de negocio.", nameof(rol))
        };
        string usuario = RutaUsuario(id);
        var grupo = await ObtenerJsonAsync<GrupoRemoto>($"group-by-path/{grupoNombre}", operacion, cancellationToken);
        if (grupo.Path != "/" + grupoNombre || !Guid.TryParseExact(grupo.Id, "D", out _))
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }

        var mapeos = await ObtenerJsonAsync<MapeosRemotos>($"groups/{grupo.Id}/role-mappings", operacion, cancellationToken);
        if (mapeos.RealmMappings is not [{ } mapeo] || mapeo.Name != rol || mapeo.ClientRole || mapeo.Composite
            || !Guid.TryParseExact(mapeo.Id, "D", out _)
            || mapeos.ClientMappings is { Count: > 0 })
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }

        await EscribirAsync(metodo, $"{usuario}/groups/{grupo.Id}", operacion, null, cancellationToken);
    }

    private async Task<T> ObtenerJsonAsync<T>(string ruta, string operacion, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, operacion, null, cancellationToken);
        TransporteKeycloak.ExigirEstado(respuesta, HttpStatusCode.OK, operacion, false);
        return await TransporteKeycloak.LeerAsync<T>(respuesta, operacion, cancellationToken);
    }

    private async Task EscribirAsync(
        HttpMethod metodo, string ruta, string operacion, object? datos, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(metodo, ruta, operacion, datos, cancellationToken);
        TransporteKeycloak.ExigirEstado(respuesta, HttpStatusCode.NoContent, operacion, true);
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string ruta, string operacion, object? datos, CancellationToken cancellationToken)
    {
        var baseUrl = opciones.ObtenerBase(operacion);
        string token = await tokens.ObtenerAsync(http, baseUrl, operacion, cancellationToken);
        using var solicitud = new HttpRequestMessage(metodo, DireccionAdmin(baseUrl, ruta));
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (datos is not null)
        {
            solicitud.Content = JsonContent.Create(datos);
        }

        var respuesta = await TransporteKeycloak.EnviarAsync(
            http, solicitud, operacion, metodo != HttpMethod.Get, cancellationToken);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            tokens.Invalidar(token);
        }

        return respuesta;
    }

    private Uri DireccionAdmin(Uri baseUrl, string ruta) => new(baseUrl, $"admin/realms/{opciones.Realm}/{ruta}");

    private static string RutaUsuario(string id) => Guid.TryParseExact(id, "D", out _)
        ? "users/" + id
        : throw new ArgumentException("El ID de Keycloak debe identificar una cuenta local.", nameof(id));

    private static CuentaKeycloak Convertir(UsuarioRemoto usuario, string operacion)
    {
        if (!Guid.TryParseExact(usuario.Id, "D", out _) || string.IsNullOrWhiteSpace(usuario.Username)
            || usuario.Enabled is null || usuario.EmailVerified is null)
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }

        Guid? correlacion = usuario.Attributes?.GetValueOrDefault(AtributoAlta) is [var valor]
            && Guid.TryParseExact(valor, "D", out var identificador) ? identificador : null;
        return new CuentaKeycloak(usuario.Id, usuario.Username, usuario.Email, usuario.FirstName, usuario.LastName,
            usuario.Enabled.Value, usuario.EmailVerified.Value, usuario.RequiredActions ?? [], correlacion);
    }

    private sealed record UsuarioRemoto(string? Id, string? Username, string? Email, string? FirstName,
        string? LastName, bool? Enabled, bool? EmailVerified, string[]? RequiredActions,
        Dictionary<string, string[]>? Attributes);
    private sealed record RolRemoto(string? Id, string? Name, bool ClientRole, bool Composite);
    private sealed record GrupoRemoto(string? Id, string? Path);
    private sealed record TipoCredencial(string? Type);
    private sealed record MetadatosOidc(string? Issuer);
    private sealed record ConflictoRemoto(string? ErrorMessage);
    private sealed record MapeosRemotos(RolRemoto[]? RealmMappings, Dictionary<string, object>? ClientMappings);
}
