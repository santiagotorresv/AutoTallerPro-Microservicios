using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Servicios;

namespace AutoTallerPro.Usuarios.Application.Fabricas;

internal sealed class CreadorCambiosCuenta : CreadorOperacionUsuario
{
    internal override EjecucionOperacionUsuario CrearOperacion(ContextoOperacionUsuario contexto,
        IIdentidadesKeycloak identidades, TimeProvider reloj) =>
        new AdministracionUsuariosServicio(identidades, reloj, contexto);
}
