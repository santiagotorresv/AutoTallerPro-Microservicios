using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Application.Persistencia;

public enum EstadoAlta
{
    Preparada = 0,
    InvitacionPendiente = 1,
    EnvioIncierto = 2,
    CorreoSolicitado = 3,
    Conflicto = 4,
    PerfilRechazado = 5
}

public sealed record AltaUsuario(Guid Id, string? Username, string? Sujeto, int? FichaId, EstadoAlta Estado);

public interface IAltasUsuarios
{
    Task<IAltaUsuario> AbrirAsync(IdentidadKeycloak actor, Guid clave, string correo, byte[] huella,
        CancellationToken cancellationToken);
}

public interface IAltaUsuario : IAsyncDisposable
{
    AltaUsuario Alta { get; }
    Task GuardarCandidatoAsync(string username, CancellationToken cancellationToken);
    Task GuardarSujetoAsync(string sujeto, CancellationToken cancellationToken);
    Task VincularFichaAsync(DateTimeOffset fecha, CancellationToken cancellationToken);
    Task GuardarEstadoAsync(EstadoAlta estado, CancellationToken cancellationToken);
}

public sealed class PersistenciaEstadoAltaException() : Exception("No se pudo confirmar el estado del alta.");

