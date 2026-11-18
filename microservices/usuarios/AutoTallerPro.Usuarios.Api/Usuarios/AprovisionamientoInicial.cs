using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Api.Usuarios;

internal static class AprovisionamientoInicial
{
    internal static async Task<int> EjecutarAsync(IServiceProvider servicios, string emisor)
    {
        try
        {
            using var tiempo = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await using var alcance = servicios.CreateAsyncScope();
            var datos = await JsonSerializer.DeserializeAsync<Solicitud>(Console.OpenStandardInput(),
                JsonSerializerOptions.Web, tiempo.Token);
            if (datos is null)
            {
                return InformarError("datos_invalidos");
            }

            var dependencias = alcance.ServiceProvider;
            var resultado = await dependencias.GetRequiredService<AltaUsuariosServicio>().AprovisionarPrimeroAsync(
                emisor, datos.Clave, datos.Email, datos.Nombre, datos.Apellido,
                dependencias.GetRequiredService<IConsultaFichasUsuarios>(), tiempo.Token);
            if (!resultado.EsExitoso)
            {
                return InformarError(AccionesAdministrativas.Codigo(resultado.Error.Codigo).ToLowerInvariant());
            }
            Console.WriteLine(JsonSerializer.Serialize(resultado.Valor, JsonSerializerOptions.Web));
            return 0;
        }
        catch (JsonException)
        {
            return InformarError("datos_invalidos");
        }
        catch (Exception error) when (error is OperacionIdentidadException or MySqlException
            or IdentidadYaVinculadaException or OperationCanceledException)
        {
            return InformarError("aprovisionamiento_pendiente");
        }
    }

    private static int InformarError(string codigo)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { code = codigo }));
        return 1;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Solicitud(Guid Clave, string? Email, string? Nombre, string? Apellido);
}
