namespace AutoTallerPro.Usuarios.Application.Identidad;

public interface IIdentidadesKeycloak
{
    Task<CuentaKeycloak?> ObtenerAsync(string id, CancellationToken cancellationToken = default);
    Task<CuentaKeycloak?> BuscarPorUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<CuentaKeycloak?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken = default);
    Task<bool> ExisteUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<string> CrearAsync(NuevaCuentaKeycloak cuenta, CancellationToken cancellationToken = default);
    Task ActualizarDatosAsync(string id, DatosCuentaKeycloak datos, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ObtenerRolesAsync(string id, CancellationToken cancellationToken = default);
    Task AsignarRolAsync(string id, string rol, CancellationToken cancellationToken = default);
    Task RetirarRolAsync(string id, string rol, CancellationToken cancellationToken = default);
    Task CambiarHabilitacionAsync(string id, bool habilitada, CancellationToken cancellationToken = default);
    Task CerrarSesionesAsync(string id, CancellationToken cancellationToken = default);
    Task SolicitarInvitacionAsync(string id, CancellationToken cancellationToken = default);
}
