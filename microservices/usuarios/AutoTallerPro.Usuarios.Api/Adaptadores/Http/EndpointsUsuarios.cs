using System.Security.Claims;
using System.Text.Json.Serialization;
using AutoTallerPro.Usuarios.Api.Seguridad;
using AutoTallerPro.Usuarios.Application.Fachadas;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AutoTallerPro.Usuarios.Api.Adaptadores.Http;

internal static class EndpointsUsuarios
{
    internal static void MapUsuarios(this WebApplication app)
    {
        var usuarios = app.MapGroup("/usuarios").RequireAuthorization(AutenticacionUsuarios.SoloAdministrador);
        usuarios.MapPost("", CrearAsync);
        usuarios.MapGet("", ListarAsync);
        usuarios.MapGet("/{id:int}", DetalleAsync);
        usuarios.MapGet("/resumen", ResumenAsync);
        usuarios.MapPut("/{id:int}", EditarAsync);
        usuarios.MapPost("/{id:int}/desactivar", DesactivarAsync);
        usuarios.MapPost("/{id:int}/reactivar", ReactivarAsync);
        usuarios.MapPost("/{id:int}/reenviar-invitacion", ReenviarAsync);
    }

    private static Task<IResult> EditarAsync(int id, [FromBody] EdicionUsuarioHttp entrada,
        [FromHeader(Name = "Idempotency-Key")] string? clave, HttpContext contexto,
        AdministracionUsuariosFacade fachada, CancellationToken cancellationToken) =>
        AdministrarAsync(clave, contexto, (actor, idempotencia) => fachada.EditarAsync(actor, id, idempotencia,
            new EdicionUsuario(entrada.Email, entrada.Rol, entrada.Activo), cancellationToken));

    private static Task<IResult> DesactivarAsync(int id, [FromHeader(Name = "Idempotency-Key")] string? clave,
        HttpContext contexto, AdministracionUsuariosFacade fachada, CancellationToken cancellationToken) =>
        AdministrarAsync(clave, contexto, (actor, idempotencia) =>
            fachada.DesactivarAsync(actor, id, idempotencia, cancellationToken));

    private static Task<IResult> ReactivarAsync(int id, [FromHeader(Name = "Idempotency-Key")] string? clave,
        HttpContext contexto, AdministracionUsuariosFacade fachada, CancellationToken cancellationToken) =>
        AdministrarAsync(clave, contexto, (actor, idempotencia) =>
            fachada.ReactivarAsync(actor, id, idempotencia, cancellationToken));

    private static Task<IResult> ReenviarAsync(int id, [FromHeader(Name = "Idempotency-Key")] string? clave,
        HttpContext contexto, AdministracionUsuariosFacade fachada, CancellationToken cancellationToken) =>
        AdministrarAsync(clave, contexto, (actor, idempotencia) =>
            fachada.ReenviarInvitacionAsync(actor, id, idempotencia, cancellationToken));

    private static async Task<IResult> AdministrarAsync(string? clave, HttpContext contexto,
        Func<IdentidadKeycloak, Guid, Task<Resultado>> ejecutar)
    {
        if (!Guid.TryParseExact(clave, "D", out var idempotencia) || idempotencia == Guid.Empty)
        {
            return ClaveInvalida();
        }

        var resultado = await ejecutar(Actor(contexto.User), idempotencia);
        return resultado.EsExitoso ? Results.NoContent() : ProblemasUsuarios.DesdeError(resultado.Error!, contexto.Response);
    }

    private static async Task<IResult> CrearAsync(
        [FromBody] AltaUsuarioHttp entrada, [FromHeader(Name = "Idempotency-Key")] string? clave,
        HttpContext contexto, AltaUsuariosServicio servicio, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(clave, "D", out var idempotencia) || idempotencia == Guid.Empty)
        {
            return ClaveInvalida();
        }

        var actor = Actor(contexto.User);
        var resultado = await servicio.CrearAsync(actor, idempotencia,
            new SolicitudAltaUsuario(entrada.Email, entrada.Rol, entrada.Nombre, entrada.Apellido), cancellationToken);
        if (!resultado.EsExitoso)
        {
            return ProblemasUsuarios.DesdeError(resultado.Error, contexto.Response);
        }

        var creado = resultado.Valor;
        string prefijo = contexto.Request.Path.Value!.EndsWith('/') ? "./" : "./usuarios/";
        return Results.Created(prefijo + creado.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), creado);
    }

    private static async Task<IResult> ListarAsync(
        HttpContext contexto, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken,
        string? busqueda, string? rol, bool? activo, int pagina = 1, int tamanoPagina = 20)
    {
        var resultado = await servicio.ListarAsync(Emisor(contexto.User), busqueda, rol, activo, pagina,
            tamanoPagina, cancellationToken);
        return resultado.EsExitoso ? Results.Ok(resultado.Valor) : ProblemasUsuarios.DesdeError(resultado.Error!, contexto.Response);
    }

    private static async Task<IResult> DetalleAsync(
        int id, HttpContext contexto, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken)
    {
        var resultado = await servicio.DetalleAsync(Emisor(contexto.User), id, cancellationToken);
        return resultado.EsExitoso ? Results.Ok(resultado.Valor) : ProblemasUsuarios.DesdeError(resultado.Error!, contexto.Response);
    }

    private static Task<ResumenUsuarios> ResumenAsync(
        ClaimsPrincipal usuario, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken) =>
        servicio.ResumenAsync(Emisor(usuario), cancellationToken);

    private static string Emisor(ClaimsPrincipal usuario) => usuario.FindFirstValue(JwtRegisteredClaimNames.Iss)!;

    private static IdentidadKeycloak Actor(ClaimsPrincipal usuario) =>
        new(Emisor(usuario), usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static IResult ClaveInvalida() =>
        ProblemasUsuarios.Respuesta(400, "clave_invalida", "Idempotency-Key debe ser un UUID.");

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record AltaUsuarioHttp(string? Email, string? Rol, string? Nombre, string? Apellido);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record EdicionUsuarioHttp(string? Email, string? Rol, bool? Activo);
}
