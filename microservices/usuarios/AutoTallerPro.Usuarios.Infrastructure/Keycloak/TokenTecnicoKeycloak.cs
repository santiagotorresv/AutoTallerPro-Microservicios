using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AutoTallerPro.Usuarios.Application.Identidad;

namespace AutoTallerPro.Usuarios.Infrastructure.Keycloak;

public sealed class TokenTecnicoKeycloak(OpcionesKeycloakAdmin opciones, TimeProvider reloj) : IDisposable
{
    private readonly SemaphoreSlim _exclusor = new(1);
    private TokenVigente? _actual;

    internal async Task<string> ObtenerAsync(
        HttpClient http, Uri baseUrl, string operacion, CancellationToken cancellationToken)
    {
        if (LeerVigente() is { } vigente)
        {
            return vigente.Valor;
        }

        await _exclusor.WaitAsync(cancellationToken);
        try
        {
            if (LeerVigente() is { } renovado)
            {
                return renovado.Valor;
            }

            var inicio = reloj.GetUtcNow();
            using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opciones.ClientId!,
                ["client_secret"] = opciones.ClientSecret!
            });
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post, new Uri(baseUrl, $"realms/{opciones.Realm}/protocol/openid-connect/token"))
            {
                Content = contenido
            };
            using var respuesta = await TransporteKeycloak.EnviarAsync(
                http, solicitud, operacion, false, cancellationToken);
            TransporteKeycloak.ExigirEstado(respuesta, System.Net.HttpStatusCode.OK, operacion, false);
            var recibido = await TransporteKeycloak.LeerAsync<RespuestaToken>(respuesta, operacion, cancellationToken);
            if (string.IsNullOrWhiteSpace(recibido.AccessToken) || recibido.ExpiresIn <= 0)
            {
                throw new OperacionIdentidadException(operacion, ErrorIdentidad.RespuestaInvalida);
            }

            var renovar = inicio.AddSeconds(recibido.ExpiresIn - Math.Min(30, recibido.ExpiresIn / 2d));
            Volatile.Write(ref _actual, new TokenVigente(recibido.AccessToken, renovar));
            return recibido.AccessToken;
        }
        finally
        {
            _exclusor.Release();
        }
    }

    internal void Invalidar(string valor)
    {
        var actual = Volatile.Read(ref _actual);
        if (actual?.Valor == valor)
        {
            Interlocked.CompareExchange(ref _actual, null, actual);
        }
    }

    private TokenVigente? LeerVigente()
    {
        var actual = Volatile.Read(ref _actual);
        return actual is not null && actual.RenovarEn > reloj.GetUtcNow() ? actual : null;
    }

    public void Dispose() => _exclusor.Dispose();

    private sealed record TokenVigente(string Valor, DateTimeOffset RenovarEn);

    private sealed record RespuestaToken(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
