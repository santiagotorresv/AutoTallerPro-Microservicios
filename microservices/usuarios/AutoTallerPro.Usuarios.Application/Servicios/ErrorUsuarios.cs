namespace AutoTallerPro.Usuarios.Application.Servicios;

public enum ErrorUsuarios
{
    AdministradorNoVigente,
    DatosInvalidos,
    PerfilRechazado,
    CorreoDuplicado,
    ClaveReutilizada,
    AltaEnCurso,
    UsernameNoDisponible,
    ReconciliacionPendiente
}

public sealed class OperacionUsuariosException(ErrorUsuarios error) : Exception(error.ToString())
{
    public ErrorUsuarios Error { get; } = error;
}

