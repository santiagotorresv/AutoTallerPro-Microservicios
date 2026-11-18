using System.Security.Claims;
using System.Text.Json.Serialization;
using AutoTallerPro.Usuarios.Api.Seguridad;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AutoTallerPro.Usuarios.Api.Usuarios;

internal static class EndpointsUsuarios
{
    internal static void MapUsuarios(this WebApplication app)
    {
        var usuarios = app.MapGroup("/usuarios").RequireAuthorization(AutenticacionUsuarios.SoloAdministrador);
        usuarios.MapPost("", CrearAsync);
        usuarios.MapGet("", ListarAsync);
        usuarios.MapGet("/{id:int}", DetalleAsync);
        usuarios.MapGet("/resumen", ResumenAsync);
    }

    private static async Task<IResult> CrearAsync(
        [FromBody] AltaUsuarioHttp entrada, [FromHeader(Name = "Idempotency-Key")] string? clave,
        HttpContext contexto, AltaUsuariosServicio servicio, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(clave, "D", out var idempotencia) || idempotencia == Guid.Empty)
        {
            return ProblemasUsuarios.Respuesta(400, "clave_invalida", "Idempotency-Key debe ser un UUID.");
        }

        var actor = new IdentidadKeycloak(Emisor(contexto.User),
            contexto.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var creado = await servicio.CrearAsync(actor, idempotencia,
            new SolicitudAltaUsuario(entrada.Email, entrada.Rol, entrada.Nombre, entrada.Apellido), cancellationToken);
        string prefijo = contexto.Request.Path.Value!.EndsWith('/') ? "./" : "./usuarios/";
        return Results.Created(prefijo + creado.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), creado);
    }

    private static async Task<PaginaUsuarios> ListarAsync(
        ClaimsPrincipal usuario, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken,
        string? busqueda, string? rol, bool? activo, int pagina = 1, int tamanoPagina = 20) =>
        await servicio.ListarAsync(Emisor(usuario), busqueda, rol, activo, pagina, tamanoPagina, cancellationToken);

    private static async Task<IResult> DetalleAsync(
        int id, ClaimsPrincipal usuario, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken)
    {
        var detalle = await servicio.DetalleAsync(Emisor(usuario), id, cancellationToken);
        return detalle is null
            ? ProblemasUsuarios.Respuesta(404, "usuario_no_encontrado", "El usuario no existe.")
            : Results.Ok(detalle);
    }

    private static Task<ResumenUsuarios> ResumenAsync(
        ClaimsPrincipal usuario, ConsultaUsuariosServicio servicio, CancellationToken cancellationToken) =>
        servicio.ResumenAsync(Emisor(usuario), cancellationToken);

    private static string Emisor(ClaimsPrincipal usuario) => usuario.FindFirstValue(JwtRegisteredClaimNames.Iss)!;

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record AltaUsuarioHttp(string? Email, string? Rol, string? Nombre, string? Apellido);
}

