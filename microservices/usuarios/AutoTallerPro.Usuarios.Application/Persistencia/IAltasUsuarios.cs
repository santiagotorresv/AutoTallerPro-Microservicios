using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;

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
public sealed record AltaInicial(Guid Clave, AltaUsuario Alta);

public interface IAltasUsuarios
{
    Task<Resultado<AltaInicial?>> BuscarAprovisionamientoAsync(IdentidadKeycloak actor, CancellationToken cancellationToken);
    Task<Resultado<IAltaUsuario>> AbrirAsync(IdentidadKeycloak actor, Guid clave, string correo, byte[] huella,
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
