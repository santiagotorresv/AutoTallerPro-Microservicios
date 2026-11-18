using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutoTallerPro.Usuarios.Tests.Persistencia;

public sealed partial class PersistenciaMySqlTests
{
    [MySqlFact]
    public async Task AdaptadorHttp_ConservaElContratoDeAltaEdicionReenvioYResultadoIncierto()
    {
        var actor = new IdentidadKeycloak(UsuariosApiFactory.Emisor, UsuariosApiFactory.Sujeto);
        var identidades = new KeycloakPrueba(actor);
        using var autenticacion = new UsuariosApiFactory();
        using var api = autenticacion.WithWebHostBuilder(builder =>
        {
            mysql.ConfigurarApi(builder);
            builder.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<IIdentidadesKeycloak>();
                servicios.AddSingleton<IIdentidadesKeycloak>(identidades);
            });
        });
        using var http = api.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            autenticacion.EmitirToken([Roles.Administrador]));
        string correo = "http-" + Guid.NewGuid().ToString("N") + "@ejemplo.invalid";
        var entrada = new { email = " " + correo.ToUpperInvariant() + " ", rol = Roles.Recepcionista, nombre = " Ana ", apellido = " Pérez " };
        var claveAlta = Guid.NewGuid();

        using var alta = await EnviarAsync(HttpMethod.Post, "/usuarios", claveAlta, entrada);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        Assert.Equal(3, cuerpo.RootElement.EnumerateObject().Count());
        int id = cuerpo.RootElement.GetProperty("id").GetInt32();
        Assert.Equal("./usuarios/" + id, alta.Headers.Location?.OriginalString);
        Assert.Equal("correoSolicitado", cuerpo.RootElement.GetProperty("estadoInvitacion").GetString());
        var cuenta = (await identidades.BuscarPorCorreoAsync(correo))!;
        Assert.Equal("Ana", cuenta.Nombre);
        Assert.Equal("Pérez", cuenta.Apellido);

        using var repetida = await EnviarAsync(HttpMethod.Post, "/usuarios", claveAlta, entrada);
        Assert.Equal(HttpStatusCode.Created, repetida.StatusCode);
        Assert.Equal(alta.Headers.Location, repetida.Headers.Location);

        using var duplicada = await EnviarAsync(HttpMethod.Post, "/usuarios", Guid.NewGuid(), entrada);
        await ComprobarAsync(duplicada, HttpStatusCode.Conflict, "correo_duplicado");

        string correoNuevo = "editado-" + correo;
        using var edicion = await EnviarAsync(HttpMethod.Put, "/usuarios/" + id, Guid.NewGuid(),
            new { email = correoNuevo, rol = Roles.Administrador, activo = true });
        Assert.Equal(HttpStatusCode.NoContent, edicion.StatusCode);
        Assert.Equal(correoNuevo, identidades.Cuentas[cuenta.Id].Correo);
        Assert.Equal([Roles.Administrador], await identidades.ObtenerRolesAsync(cuenta.Id));

        string reenvio = $"/usuarios/{id}/reenviar-invitacion";
        using var posterior = await EnviarAsync(HttpMethod.Post, reenvio, Guid.NewGuid());
        await ComprobarAsync(posterior, HttpStatusCode.TooManyRequests, "intervalo_invitacion");
        Assert.InRange(posterior.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0, 1, 120);

        await using var conexion = mysql.CrearConexion();
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = """
            UPDATE ControlInvitacionesUsuarios SET UltimoIntentoUtc = UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE WHERE FichaId = @Id;
            """;
        comando.Parameters.AddWithValue("@Id", id);
        await comando.ExecuteNonQueryAsync();
        identidades.ErrorCorreo = new("envío", ErrorIdentidad.TiempoAgotado, resultadoIncierto: true);

        var claveEnvio = Guid.NewGuid();
        using var incierta = await EnviarAsync(HttpMethod.Post, reenvio, claveEnvio);
        await ComprobarAsync(incierta, HttpStatusCode.ServiceUnavailable, "envio_incierto");
        using var recuperada = await EnviarAsync(HttpMethod.Post, reenvio, claveEnvio);
        await ComprobarAsync(recuperada, HttpStatusCode.ServiceUnavailable, "envio_incierto");
        Assert.Equal(2, identidades.SolicitudesCorreo);

        async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, Guid clave, object? datos = null)
        {
            using var solicitud = new HttpRequestMessage(metodo, ruta);
            solicitud.Headers.Add("Idempotency-Key", clave.ToString("D"));
            if (datos is not null)
            {
                solicitud.Content = JsonContent.Create(datos);
            }
            return await http.SendAsync(solicitud);
        }
    }

    private static async Task ComprobarAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(codigo, cuerpo.RootElement.GetProperty("code").GetString());
    }
}
