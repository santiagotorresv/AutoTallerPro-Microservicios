using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Servicios;

internal static class ValidacionAdministrador
{
    internal static async Task<Resultado> ValidarAsync(IIdentidadesKeycloak identidades, IdentidadKeycloak actor,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParseExact(actor.Sujeto, "D", out _))
        {
            try
            {
                var cuenta = await identidades.ObtenerAsync(actor.Sujeto, cancellationToken);
                if (cuenta is not null && PerfilListo(cuenta)
                    && (await identidades.ObtenerRolesAsync(actor.Sujeto, cancellationToken)).Contains(Roles.Administrador))
                {
                    return Resultado.Exitoso();
                }
            }
            catch (OperacionIdentidadException error) when (error.Error is ErrorIdentidad.Prohibido or ErrorIdentidad.NoEncontrado)
            {
            }
        }

        return Resultado.Fallido(ErrorUsuarios.AdministradorNoVigente);
    }

    internal static bool PerfilListo(CuentaKeycloak cuenta) =>
        cuenta.Habilitada && cuenta.CorreoVerificado && cuenta.AccionesRequeridas.Count == 0;
}
