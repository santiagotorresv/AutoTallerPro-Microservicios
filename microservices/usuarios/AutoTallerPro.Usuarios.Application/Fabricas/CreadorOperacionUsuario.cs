using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Fabricas;

internal abstract class CreadorOperacionUsuario
{
    internal abstract EjecucionOperacionUsuario CrearOperacion(ContextoOperacionUsuario contexto,
        IIdentidadesKeycloak identidades, TimeProvider reloj);
}

internal sealed record ContextoOperacionUsuario(IdentidadKeycloak Actor, int FichaId, TipoAdministracion Tipo,
    EdicionUsuario Datos, CuentaKeycloak Cuenta);

internal abstract class EjecucionOperacionUsuario
{
    internal abstract Task<Resultado<CambiosUsuario>> PrepararAsync(ISesionAdministracionUsuarios sesion,
        OperacionAdministrativa? operacion, CancellationToken cancellationToken);

    internal abstract Task<Resultado> EjecutarAsync(ISesionAdministracionUsuarios sesion,
        OperacionAdministrativa operacion, CancellationToken cancellationToken);
}
