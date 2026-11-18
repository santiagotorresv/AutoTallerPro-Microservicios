using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Application.Persistencia;

public interface IFichasAdministrativas
{
    Task<int> CrearAsync(
        IdentidadKeycloak identidad, DateTimeOffset fechaCreacion,
        IdentidadKeycloak? actor, CancellationToken cancellationToken = default);

    Task<FichaAdministrativa?> BuscarPorIdAsync(int id, CancellationToken cancellationToken = default);

    Task<FichaAdministrativa?> BuscarPorIdentidadAsync(
        IdentidadKeycloak identidad, CancellationToken cancellationToken = default);

    Task<bool> ActualizarMetadatosAsync(
        int id, DateTimeOffset fechaModificacion,
        IdentidadKeycloak? actor, CancellationToken cancellationToken = default);
}
