using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Fabricas;

internal sealed class CreadorReenvioInvitacion : CreadorOperacionUsuario
{
    internal override EjecucionOperacionUsuario CrearOperacion(ContextoOperacionUsuario contexto,
        IIdentidadesKeycloak identidades, TimeProvider reloj) =>
        new ReenvioInvitacion(identidades, reloj, contexto);

    private sealed class ReenvioInvitacion(IIdentidadesKeycloak identidades, TimeProvider reloj,
        ContextoOperacionUsuario contexto) : EjecucionOperacionUsuario
    {
        private static readonly TimeSpan IntervaloEnvio = TimeSpan.FromMinutes(2);
        private IReadOnlyList<AccionInvitacion> _acciones = [];

        internal override async Task<Resultado<CambiosUsuario>> PrepararAsync(ISesionAdministracionUsuarios sesion,
            OperacionAdministrativa? operacion, CancellationToken cancellationToken)
        {
            if (operacion?.Paso is null or CambiosUsuario.Ninguno)
            {
                _acciones = await AccionesPendientesAsync(contexto.Cuenta, cancellationToken);
                if (_acciones.Count == 0)
                {
                    return Resultado<CambiosUsuario>.Fallido(ErrorUsuarios.InvitacionNoElegible);
                }

                var envio = await sesion.ObtenerInvitacionAsync(contexto.FichaId, cancellationToken);
                if (envio?.UltimoIntentoUtc is { } ultimo)
                {
                    var espera = ultimo + IntervaloEnvio - envio.AhoraUtc;
                    if (espera > TimeSpan.Zero)
                    {
                        return Resultado<CambiosUsuario>.Fallido(new ErrorResultado(ErrorUsuarios.IntervaloInvitacion,
                            (int)Math.Ceiling(espera.TotalSeconds)));
                    }
                }
            }

            return Resultado<CambiosUsuario>.Exitoso(CambiosUsuario.Invitacion);
        }

        internal override async Task<Resultado> EjecutarAsync(ISesionAdministracionUsuarios sesion,
            OperacionAdministrativa operacion, CancellationToken cancellationToken)
        {
            if (operacion.Paso != CambiosUsuario.Ninguno)
            {
                return await CerrarEnvioAsync(EstadoOperacion.EnvioIncierto, EstadoAlta.EnvioIncierto,
                    ErrorUsuarios.EnvioInvitacionIncierto);
            }

            await sesion.ReservarInvitacionAsync(operacion.FichaId, operacion.Id, cancellationToken);
            operacion = operacion with { Paso = CambiosUsuario.Invitacion };
            await sesion.GuardarPasoAsync(operacion, false, cancellationToken);

            try
            {
                await identidades.SolicitarInvitacionAsync(contexto.Cuenta.Id, _acciones, cancellationToken);
            }
            catch (OperacionIdentidadException error)
            {
                return error.ResultadoIncierto
                    ? await CerrarEnvioAsync(EstadoOperacion.EnvioIncierto, EstadoAlta.EnvioIncierto,
                        ErrorUsuarios.EnvioInvitacionIncierto)
                    : await CerrarEnvioAsync(EstadoOperacion.Fallida, EstadoAlta.InvitacionPendiente,
                        ErrorUsuarios.EnvioInvitacionFallido);
            }

            return await CerrarEnvioAsync(EstadoOperacion.Exitosa, EstadoAlta.CorreoSolicitado, null);

            async Task<Resultado> CerrarEnvioAsync(EstadoOperacion estado, EstadoAlta invitacion, ErrorUsuarios? error)
            {
                await sesion.CerrarOperacionAsync(operacion with
                {
                    Estado = estado,
                    Error = error,
                    Restantes = CambiosUsuario.Ninguno,
                    Paso = CambiosUsuario.Ninguno
                }, reloj.GetUtcNow(), false, invitacion, cancellationToken);
                return error is { } codigo ? Resultado.Fallido(codigo) : Resultado.Exitoso();
            }
        }

        private async Task<IReadOnlyList<AccionInvitacion>> AccionesPendientesAsync(CuentaKeycloak cuenta,
            CancellationToken cancellationToken)
        {
            var acciones = new List<AccionInvitacion>();
            if (cuenta.Habilitada)
            {
                if (!cuenta.CorreoVerificado || cuenta.AccionesRequeridas.Contains(nameof(AccionInvitacion.VERIFY_EMAIL)))
                {
                    acciones.Add(AccionInvitacion.VERIFY_EMAIL);
                }
                if (cuenta.AccionesRequeridas.Contains(nameof(AccionInvitacion.UPDATE_PASSWORD))
                    || !await identidades.TieneContrasenaAsync(cuenta.Id, cancellationToken))
                {
                    acciones.Add(AccionInvitacion.UPDATE_PASSWORD);
                }
            }
            return acciones;
        }
    }
}
