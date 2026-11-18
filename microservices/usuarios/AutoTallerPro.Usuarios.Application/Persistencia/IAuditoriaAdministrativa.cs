using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Application.Persistencia;

public interface IAuditoriaAdministrativa
{
    Task<long> RegistrarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegistroAuditoria>> ObtenerRecientesAsync(
        int cantidad, CancellationToken cancellationToken = default);
}
