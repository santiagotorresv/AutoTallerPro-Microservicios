using System.Text.RegularExpressions;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public static partial class AccionesAdministrativas
{
    public static string Nombre(TipoAdministracion tipo) => tipo switch
    {
        TipoAdministracion.Editar => "USUARIO_EDITADO",
        TipoAdministracion.Desactivar => "USUARIO_DESACTIVADO",
        TipoAdministracion.Reactivar => "USUARIO_REACTIVADO",
        TipoAdministracion.Reenviar => "INVITACION_REENVIADA",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    public static string Codigo(ErrorUsuarios error) => LimitePalabra().Replace(error.ToString(), "_").ToUpperInvariant();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex LimitePalabra();
}
