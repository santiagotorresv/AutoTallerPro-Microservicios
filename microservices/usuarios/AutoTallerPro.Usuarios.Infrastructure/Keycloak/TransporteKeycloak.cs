using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;

namespace AutoTallerPro.Usuarios.Infrastructure.Keycloak;

internal static class TransporteKeycloak
{
    internal static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient http, HttpRequestMessage solicitud, string operacion, bool escritura,
        CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(solicitud, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.TiempoAgotado, resultadoIncierto: escritura);
        }
        catch (HttpRequestException)
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.NoDisponible, resultadoIncierto: escritura);
        }
    }

    internal static void ExigirEstado(HttpResponseMessage respuesta, HttpStatusCode esperado, string operacion, bool escritura)
    {
        if (respuesta.StatusCode == esperado)
        {
            return;
        }

        int estado = (int)respuesta.StatusCode;
        var error = estado switch
        {
            401 => ErrorIdentidad.NoAutorizado,
            403 => ErrorIdentidad.Prohibido,
            404 => ErrorIdentidad.NoEncontrado,
            409 => ErrorIdentidad.Conflicto,
            408 or 504 => ErrorIdentidad.TiempoAgotado,
            429 or >= 500 => ErrorIdentidad.NoDisponible,
            >= 400 => ErrorIdentidad.SolicitudRechazada,
            _ => ErrorIdentidad.RespuestaInvalida
        };
        throw new OperacionIdentidadException(operacion, error, estado,
            resultadoIncierto: escritura && (estado < 400 || estado == 408 || estado >= 500));
    }

    internal static async Task<T> LeerAsync<T>(
        HttpResponseMessage respuesta, string operacion, CancellationToken cancellationToken)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }
        catch (JsonException)
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
        }
    }
}
