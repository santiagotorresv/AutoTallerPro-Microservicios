using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
namespace AutoTallerPro.Frontend.GrupoUno;

public sealed record RespuestaApi<T>(T? Valor, string? Error) { public bool Exito => Error is null; }
public sealed class ClienteGateway(IConfiguration configuracion, HttpContext contexto)
{
    private static readonly HttpClient http = new(new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = TimeSpan.FromSeconds(20) };
    public async Task<RespuestaApi<T>> EnviarAsync<T>(HttpMethod metodo, string ruta, object? datos = null)
    {
        if (contexto.RequestServices.GetService<IAuthenticationSchemeProvider>() is not { } esquemas ||
            await esquemas.GetDefaultAuthenticateSchemeAsync() is null)
            return new(default, "El inicio de sesión aún debe integrarse con el módulo de Usuarios. No se pudo obtener una sesión autenticada.");
        var token = await contexto.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) return new(default, "Inicie sesión para consultar o modificar los datos.");
        var direccion = configuracion["GrupoUno:GatewayUrl"] ??
            (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ? "http://gateway:8080/" : "http://127.0.0.1:5100/");
        using var solicitud = new HttpRequestMessage(metodo, new Uri(new Uri(direccion.TrimEnd('/') + "/"), ruta));
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (datos is not null) solicitud.Content = JsonContent.Create(datos);
        try
        {
            using var respuesta = await http.SendAsync(solicitud, contexto.RequestAborted);
            if (respuesta.StatusCode == HttpStatusCode.Unauthorized) return new(default, "La sesión ha expirado. Inicie sesión nuevamente.");
            if (respuesta.StatusCode == HttpStatusCode.Forbidden) return new(default, "Su cuenta no tiene permiso para realizar esta operación.");
            if (!respuesta.IsSuccessStatusCode)
            {
                var mensaje = "No se pudo completar la operación. Intente nuevamente.";
                try
                {
                    var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>(contexto.RequestAborted);
                    if ((int)respuesta.StatusCode < 500 && problema.TryGetProperty("title", out var titulo)) mensaje = titulo.GetString() ?? mensaje;
                }
                catch (JsonException) { }
                return new(default, mensaje);
            }
            return new(await respuesta.Content.ReadFromJsonAsync<T>(contexto.RequestAborted), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return new(default, "El servicio no está disponible en este momento. Intente nuevamente."); }
    }
}
