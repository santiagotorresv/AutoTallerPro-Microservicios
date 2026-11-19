using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace AutoTallerPro.Frontend.ApiUsuarios;

// Adapter del Frontend: convierte las llamadas de las páginas en solicitudes HTTP al Gateway
// y las respuestas de la API (JSON, ProblemDetails y Retry-After) en resultados para la interfaz.
public sealed class ClienteUsuarios(HttpClient http, IHttpContextAccessor accesor)
{
    private const string Prefijo = "api/usuarios/usuarios";

    public Task<RespuestaUsuarios<PaginaUsuarios>> ListarAsync(
        string? busqueda, string? rol, bool? activo, int pagina, int tamanoPagina)
    {
        var filtros = new Dictionary<string, string?>
        {
            ["busqueda"] = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim(),
            ["rol"] = string.IsNullOrWhiteSpace(rol) ? null : rol,
            ["activo"] = activo?.ToString().ToLowerInvariant(),
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
            ["tamanoPagina"] = tamanoPagina.ToString(CultureInfo.InvariantCulture)
        };
        return EnviarAsync<PaginaUsuarios>(HttpMethod.Get, QueryHelpers.AddQueryString(Prefijo, filtros));
    }

    public Task<RespuestaUsuarios<UsuarioApi>> DetalleAsync(int id) =>
        EnviarAsync<UsuarioApi>(HttpMethod.Get, $"{Prefijo}/{id}");

    public Task<RespuestaUsuarios<ResumenUsuarios>> ResumenAsync() =>
        EnviarAsync<ResumenUsuarios>(HttpMethod.Get, $"{Prefijo}/resumen");

    public Task<RespuestaUsuarios<UsuarioCreado>> CrearAsync(
        string email, string rol, string nombre, string apellido, Guid clave) =>
        EnviarAsync<UsuarioCreado>(HttpMethod.Post, Prefijo, new { email, rol, nombre, apellido }, clave);

    public Task<RespuestaUsuarios<bool>> EditarAsync(int id, string email, string rol, bool activo, Guid clave) =>
        EnviarAsync<bool>(HttpMethod.Put, $"{Prefijo}/{id}", new { email, rol, activo }, clave);

    public Task<RespuestaUsuarios<bool>> DesactivarAsync(int id, Guid clave) =>
        EnviarAsync<bool>(HttpMethod.Post, $"{Prefijo}/{id}/desactivar", null, clave);

    public Task<RespuestaUsuarios<bool>> ReactivarAsync(int id, Guid clave) =>
        EnviarAsync<bool>(HttpMethod.Post, $"{Prefijo}/{id}/reactivar", null, clave);

    public Task<RespuestaUsuarios<bool>> ReenviarInvitacionAsync(int id, Guid clave) =>
        EnviarAsync<bool>(HttpMethod.Post, $"{Prefijo}/{id}/reenviar-invitacion", null, clave);

    private async Task<RespuestaUsuarios<T>> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo = null, Guid? clave = null)
    {
        var contexto = accesor.HttpContext;
        var token = contexto is null ? null : await contexto.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            return new(default, 401, null, Mensaje(401, null, null));
        }

        using var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (clave is { } idempotencia)
        {
            solicitud.Headers.Add("Idempotency-Key", idempotencia.ToString("D"));
        }

        if (cuerpo is not null)
        {
            solicitud.Content = JsonContent.Create(cuerpo);
        }

        try
        {
            var cancelacion = contexto?.RequestAborted ?? CancellationToken.None;
            using var respuesta = await http.SendAsync(solicitud, cancelacion);
            var estado = (int)respuesta.StatusCode;
            if (respuesta.IsSuccessStatusCode)
            {
                var valor = respuesta.Content.Headers.ContentLength == 0 || estado == 204
                    ? default
                    : await respuesta.Content.ReadFromJsonAsync<T>(cancelacion);
                return new(valor, estado, null, null);
            }

            var (codigo, titulo) = await LeerProblemaAsync(respuesta, cancelacion);
            int? espera = respuesta.Headers.RetryAfter?.Delta is { } delta ? (int)delta.TotalSeconds : null;
            return new(default, estado, codigo, Mensaje(estado, codigo, titulo, espera), espera);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(default, 0, null, Mensaje(0, null, null));
        }
    }

    private static async Task<(string? Codigo, string? Titulo)> LeerProblemaAsync(
        HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.Content.Headers.ContentType?.MediaType?.Contains("json") != true)
        {
            return (null, null);
        }

        try
        {
            var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>(cancelacion);
            return (Texto(problema, "code"), Texto(problema, "title"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? Texto(JsonElement json, string propiedad) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(propiedad, out var valor) &&
        valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    public static string Mensaje(int estado, string? codigo, string? titulo, int? espera = null) => codigo switch
    {
        "clave_invalida" => "La solicitud no tiene una clave de operación válida. Vuelva a intentarlo.",
        "datos_invalidos" or "solicitud_invalida" => "Revise los datos ingresados.",
        "perfil_rechazado" => "Keycloak rechazó los datos del perfil. Revise el nombre, el apellido y el correo.",
        "administrador_no_vigente" => "Su cuenta ya no tiene permisos de administrador vigentes. Cierre sesión e ingrese nuevamente.",
        "usuario_no_encontrado" => "El usuario ya no existe.",
        "correo_duplicado" => "El correo ya está asociado a otro usuario o a un alta pendiente.",
        "username_no_disponible" => "No se encontró un nombre de usuario disponible para ese correo.",
        "clave_reutilizada" => "La operación anterior se registró con otros datos. Envíe el formulario nuevamente.",
        "alta_en_curso" => "El alta todavía está en curso. Espere un momento y reintente.",
        "administracion_en_curso" => "Hay otra operación administrativa en curso. Espere un momento y reintente.",
        "cuenta_propia" => "No puede desactivar su propia cuenta ni quitarse el rol Administrador.",
        "ultimo_administrador" => "Debe quedar al menos otro administrador operativo.",
        "estado_sin_cambio" => "La cuenta ya se encontraba en ese estado.",
        "rol_fuera_de_gestion" => "El rol de esta cuenta se asignó fuera del sistema y debe revisarse en Keycloak.",
        "invitacion_no_elegible" => "La cuenta no tiene acciones pendientes o está desactivada. No se puede reenviar la invitación.",
        "intervalo_invitacion" => $"Debe esperar {espera ?? 120} segundos antes de reenviar la invitación.",
        "alta_pendiente" => "El alta quedó pendiente de confirmación. Reintente la misma operación; si persiste, avise al responsable de Usuarios.",
        "administracion_pendiente" => "El cambio quedó pendiente de confirmación. Reintente la misma operación; si persiste, avise al responsable de Usuarios.",
        "dependencia_no_disponible" => "El servicio de usuarios no pudo comunicarse con sus dependencias. Reintente la misma operación.",
        "envio_incierto" => "El correo pudo haberse enviado. No se volverá a enviar automáticamente; revise la bandeja antes de reenviar.",
        "envio_fallido" => "Keycloak rechazó el envío del correo. Podrá reenviar la invitación más tarde.",
        _ => estado switch
        {
            401 => "Su sesión expiró. Cierre sesión e ingrese nuevamente.",
            403 => "Su cuenta no tiene permiso para realizar esta operación.",
            404 => "El usuario solicitado no existe.",
            429 => $"Debe esperar {espera ?? 120} segundos antes de reintentar.",
            >= 400 and < 500 when titulo is not null => titulo,
            _ => "No se pudo confirmar el resultado. Reintente la misma operación."
        }
    };
}
