using System.Globalization;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain.Resultados;
using Microsoft.AspNetCore.Diagnostics;
using MySqlConnector;

namespace AutoTallerPro.Usuarios.Api.Adaptadores.Http;

internal sealed class ProblemasUsuarios : IExceptionHandler
{
    internal static IResult Respuesta(int estado, string codigo, string titulo) =>
        Results.Problem(statusCode: estado, title: titulo, extensions: new Dictionary<string, object?> { ["code"] = codigo });

    internal static IResult DesdeError(ErrorResultado error, HttpResponse respuesta)
    {
        var problema = error.Codigo switch
        {
            ErrorUsuarios.AdministradorNoVigente => Respuesta(403, "administrador_no_vigente", "Se requiere un administrador vigente."),
            ErrorUsuarios.DatosInvalidos => Respuesta(400, "datos_invalidos", "Revisa los campos o parámetros de la solicitud."),
            ErrorUsuarios.PerfilRechazado => Respuesta(400, "perfil_rechazado", "Los datos del perfil no son válidos."),
            ErrorUsuarios.CorreoDuplicado => Respuesta(409, "correo_duplicado", "El correo ya está asociado a un usuario o a un alta pendiente."),
            ErrorUsuarios.ClaveReutilizada => Respuesta(409, "clave_reutilizada", "La clave corresponde a otra solicitud."),
            ErrorUsuarios.AltaEnCurso => Respuesta(409, "alta_en_curso", "El alta está en curso. Reintenta con la misma clave."),
            ErrorUsuarios.UsernameNoDisponible => Respuesta(409, "username_no_disponible", "No se encontró un nombre de usuario disponible."),
            ErrorUsuarios.ReconciliacionPendiente => Respuesta(503, "alta_pendiente", "El alta requiere reconciliación. Conserva la misma clave."),
            ErrorUsuarios.UsuarioNoEncontrado => Respuesta(404, "usuario_no_encontrado", "El usuario no existe."),
            ErrorUsuarios.AdministracionEnCurso => Respuesta(409, "administracion_en_curso", "Otra operación administrativa está en curso."),
            ErrorUsuarios.CuentaPropia => Respuesta(409, "cuenta_propia", "No puedes desactivar tu cuenta ni quitarte el rol Administrador."),
            ErrorUsuarios.UltimoAdministrador => Respuesta(409, "ultimo_administrador", "Debe quedar otro administrador operativo."),
            ErrorUsuarios.EstadoSinCambio => Respuesta(409, "estado_sin_cambio", "La cuenta ya está en el estado solicitado."),
            ErrorUsuarios.RolFueraDeGestion => Respuesta(409, "rol_fuera_de_gestion", "El rol actual requiere revisión en Keycloak."),
            ErrorUsuarios.InvitacionNoElegible => Respuesta(409, "invitacion_no_elegible", "La cuenta no admite un reenvío de invitación."),
            ErrorUsuarios.IntervaloInvitacion => Respuesta(429, "intervalo_invitacion", "Espera antes de solicitar otra invitación."),
            ErrorUsuarios.EnvioInvitacionIncierto => Respuesta(503, "envio_incierto", "El envío pudo haberse realizado. No se repetirá automáticamente."),
            ErrorUsuarios.EnvioInvitacionFallido => Respuesta(503, "envio_fallido", "Keycloak rechazó la solicitud de invitación."),
            ErrorUsuarios.AdministracionPendiente => Respuesta(503, "administracion_pendiente", "El cambio pudo haberse aplicado. Conserva la clave para reconciliarlo."),
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };

        if (error.SegundosEspera is { } segundos)
        {
            respuesta.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        }

        return problema;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception error, CancellationToken cancellationToken)
    {
        IResult? respuesta = error switch
        {
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
