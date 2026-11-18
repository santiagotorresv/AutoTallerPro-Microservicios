using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using Microsoft.AspNetCore.Diagnostics;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Api.Usuarios;

internal sealed class ProblemasUsuarios : IExceptionHandler
{
    internal static IResult Respuesta(int estado, string codigo, string titulo) =>
        Results.Problem(statusCode: estado, title: titulo, extensions: new Dictionary<string, object?> { ["code"] = codigo });

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception error, CancellationToken cancellationToken)
    {
        IResult? respuesta = error switch
        {
            OperacionUsuariosException operacion => operacion.Error switch
            {
                ErrorUsuarios.AdministradorNoVigente => Respuesta(403, "administrador_no_vigente", "Se requiere un administrador vigente."),
                ErrorUsuarios.DatosInvalidos => Respuesta(400, "datos_invalidos", "Revisa los campos o parámetros de la solicitud."),
                ErrorUsuarios.PerfilRechazado => Respuesta(400, "perfil_rechazado", "Los datos del perfil no son válidos."),
                ErrorUsuarios.CorreoDuplicado => Respuesta(409, "correo_duplicado", "El correo ya está asociado a un usuario o a un alta pendiente."),
                ErrorUsuarios.ClaveReutilizada => Respuesta(409, "clave_reutilizada", "La clave corresponde a otra solicitud."),
                ErrorUsuarios.AltaEnCurso => Respuesta(409, "alta_en_curso", "El alta está en curso. Reintenta con la misma clave."),
                ErrorUsuarios.UsernameNoDisponible => Respuesta(409, "username_no_disponible", "No se encontró un nombre de usuario disponible."),
                ErrorUsuarios.ReconciliacionPendiente => Respuesta(503, "alta_pendiente", "El alta requiere reconciliación. Conserva la misma clave."),
                _ => null
            },
            BadHttpRequestException solicitud => Respuesta(solicitud.StatusCode, "solicitud_invalida", "La solicitud es inválida."),
            OperacionIdentidadException or MySqlException or IdentidadYaVinculadaException => Respuesta(503, "dependencia_no_disponible", "Usuarios no pudo acceder a una dependencia."),
            _ => null
        };

        if (respuesta is null)
        {
            return false;
        }

        await respuesta.ExecuteAsync(contexto);
        return true;
    }
}

