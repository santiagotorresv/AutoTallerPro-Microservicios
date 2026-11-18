using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Persistencia;

public enum TipoAdministracion { Editar = 1, Desactivar, Reactivar, Reenviar }
public enum EstadoOperacion { Pendiente, Exitosa, Fallida, EnvioIncierto }

[Flags]
public enum CambiosUsuario
{
    Ninguno = 0, Correo = 1, AgregarRol = 2, RetirarRol = 4, Habilitacion = 8, Sesiones = 16, Invitacion = 32
}

public sealed record OperacionAdministrativa(
    Guid Id, Guid Clave, IdentidadKeycloak Actor, int FichaId, TipoAdministracion Tipo, byte[] Huella,
    CambiosUsuario Cambios, CambiosUsuario Restantes, CambiosUsuario Paso, EstadoOperacion Estado,
    ErrorUsuarios? Error = null);

public sealed record ControlInvitacion(
    Guid? ReservadoPor, DateTimeOffset? UltimoIntentoUtc, EstadoAlta Estado, DateTimeOffset AhoraUtc);

public interface IAdministracionUsuarios
{
    Task<Resultado<ISesionAdministracionUsuarios>> AbrirAsync(string emisor, CancellationToken cancellationToken);
}

public interface ISesionAdministracionUsuarios : IAsyncDisposable
{
    Task<OperacionAdministrativa?> BuscarAsync(Guid clave, CancellationToken cancellationToken);
    Task<bool> HayEdicionPendienteAsync(Guid excepto, CancellationToken cancellationToken);
    Task AbrirOperacionAsync(OperacionAdministrativa operacion, DateTimeOffset fecha, CancellationToken cancellationToken);
    Task GuardarPasoAsync(OperacionAdministrativa operacion, bool correoCambiado, CancellationToken cancellationToken);
    Task CerrarOperacionAsync(OperacionAdministrativa operacion, DateTimeOffset fecha, bool modificarFicha,
        EstadoAlta? estadoInvitacion, CancellationToken cancellationToken);
    Task<ControlInvitacion?> ObtenerInvitacionAsync(int fichaId, CancellationToken cancellationToken);
    Task ReservarInvitacionAsync(int fichaId, Guid operacion, CancellationToken cancellationToken);
    Task GuardarInvitacionAsync(int fichaId, Guid operacion, EstadoAlta estado, CancellationToken cancellationToken);
}
