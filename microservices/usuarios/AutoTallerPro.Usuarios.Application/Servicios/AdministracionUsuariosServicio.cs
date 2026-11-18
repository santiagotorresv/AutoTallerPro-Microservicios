using AutoTallerPro.Usuarios.Application.Fabricas;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public sealed record EdicionUsuario(string? Email, string? Rol, bool? Activo);

internal sealed class AdministracionUsuariosServicio(IIdentidadesKeycloak identidades, TimeProvider reloj,
    ContextoOperacionUsuario contexto) : EjecucionOperacionUsuario
{
    private const string GrupoAdministrador = "/usuarios-rol-administrador";
    private const string GrupoRecepcionista = "/usuarios-rol-recepcionista";
    private static readonly CambiosUsuario[] PasosEdicion =
    [
        CambiosUsuario.Correo, CambiosUsuario.AgregarRol, CambiosUsuario.RetirarRol,
        CambiosUsuario.Habilitacion, CambiosUsuario.Sesiones
    ];

    internal override async Task<Resultado<CambiosUsuario>> PrepararAsync(ISesionAdministracionUsuarios sesion,
        OperacionAdministrativa? operacion, CancellationToken cancellationToken)
    {
        if (operacion is not null)
        {
            return Resultado<CambiosUsuario>.Exitoso(operacion.Cambios);
        }

        var datos = contexto.Datos;
        var cuenta = contexto.Cuenta;
        if (contexto.Tipo != TipoAdministracion.Editar && datos.Activo == cuenta.Habilitada)
        {
            return Resultado<CambiosUsuario>.Fallido(ErrorUsuarios.EstadoSinCambio);
        }

        if (contexto.Actor.Sujeto == cuenta.Id && (datos.Activo == false || datos.Rol == Roles.Recepcionista))
        {
            return Resultado<CambiosUsuario>.Fallido(ErrorUsuarios.CuentaPropia);
        }

        var plan = CambiosUsuario.Ninguno;
        if (datos.Activo is { } activo && activo != cuenta.Habilitada)
        {
            plan |= CambiosUsuario.Habilitacion;
            if (!activo)
            {
                plan |= CambiosUsuario.Sesiones;
            }
        }

        if (datos.Email is { } email && !CorreoCoincide(cuenta, email))
        {
            var duplicada = await identidades.BuscarPorCorreoAsync(email, cancellationToken);
            if (duplicada is not null && duplicada.Id != cuenta.Id)
            {
                return Resultado<CambiosUsuario>.Fallido(ErrorUsuarios.CorreoDuplicado);
            }
            plan |= CambiosUsuario.Correo;
        }

        var roles = await identidades.ObtenerRolesAsync(cuenta.Id, cancellationToken);
        if (datos.Rol is { } rol)
        {
            if (!roles.Contains(rol))
            {
                plan |= CambiosUsuario.AgregarRol;
            }
            string anterior = RolOpuesto(rol);
            if (roles.Contains(anterior))
            {
                var directos = await identidades.ObtenerRolesDirectosAsync(cuenta.Id, cancellationToken);
                var grupos = await identidades.ObtenerGruposAsync(cuenta.Id, cancellationToken);
                string grupo = anterior == Roles.Administrador ? GrupoAdministrador : GrupoRecepcionista;
                if (directos.Contains(anterior) || !grupos.Contains(grupo))
                {
                    return Resultado<CambiosUsuario>.Fallido(ErrorUsuarios.RolFueraDeGestion);
                }
                plan |= CambiosUsuario.RetirarRol;
                if (anterior == Roles.Administrador)
                {
                    plan |= CambiosUsuario.Sesiones;
                }
            }
        }

        var proteccion = await ProtegerAdministradorAsync(cuenta, roles, plan, datos, cancellationToken);
        return proteccion.EsExitoso ? Resultado<CambiosUsuario>.Exitoso(plan)
            : Resultado<CambiosUsuario>.Fallido(proteccion.Error!);
    }

    private async Task<Resultado> ProtegerAdministradorAsync(CuentaKeycloak objetivo, IReadOnlyList<string> roles,
        CambiosUsuario pasos, EdicionUsuario datos, CancellationToken cancellationToken)
    {
        bool riesgo = pasos.HasFlag(CambiosUsuario.Correo)
            || (pasos.HasFlag(CambiosUsuario.Habilitacion) && datos.Activo == false)
            || (pasos.HasFlag(CambiosUsuario.RetirarRol) && datos.Rol == Roles.Recepcionista);
        if (!riesgo || !roles.Contains(Roles.Administrador) || !ValidacionAdministrador.PerfilListo(objetivo)
            || !await identidades.TieneContrasenaAsync(objetivo.Id, cancellationToken))
        {
            return Resultado.Exitoso();
        }

        foreach (var otra in await identidades.ListarAdministradasAsync(cancellationToken))
        {
            if (otra.Id != objetivo.Id && ValidacionAdministrador.PerfilListo(otra)
                && (await identidades.ObtenerRolesAsync(otra.Id, cancellationToken)).Contains(Roles.Administrador)
                && await identidades.TieneContrasenaAsync(otra.Id, cancellationToken))
            {
                return Resultado.Exitoso();
            }
        }
        return Resultado.Fallido(ErrorUsuarios.UltimoAdministrador);
    }

    internal override async Task<Resultado> EjecutarAsync(ISesionAdministracionUsuarios sesion,
        OperacionAdministrativa operacion, CancellationToken cancellationToken)
    {
        var datos = contexto.Datos;
        string sujeto = contexto.Cuenta.Id;
        foreach (var paso in PasosEdicion.Where(paso => operacion.Restantes.HasFlag(paso)))
        {
            var cuenta = await identidades.ObtenerAsync(sujeto, cancellationToken);
            if (cuenta is null)
            {
                return Resultado.Fallido(ErrorUsuarios.AdministracionPendiente);
            }

            if (operacion.Paso != CambiosUsuario.Ninguno)
            {
                if (operacion.Paso != paso || !await PasoConfirmadoAsync(cuenta, datos, paso, cancellationToken))
                {
                    return Resultado.Fallido(ErrorUsuarios.AdministracionPendiente);
                }
            }
            else
            {
                bool riesgo = paso is CambiosUsuario.Correo or CambiosUsuario.Habilitacion or CambiosUsuario.RetirarRol;
                if (riesgo)
                {
                    var roles = await identidades.ObtenerRolesAsync(sujeto, cancellationToken);
                    var proteccion = await ProtegerAdministradorAsync(cuenta, roles, paso, datos, cancellationToken);
                    if (!proteccion.EsExitoso)
                    {
                        return proteccion;
                    }
                }

                operacion = operacion with { Paso = paso };
                await sesion.GuardarPasoAsync(operacion, false, cancellationToken);
                try
                {
                    await AplicarPasoAsync(cuenta, datos, paso, cancellationToken);
                }
                catch (OperacionIdentidadException error) when (!error.ResultadoIncierto)
                {
                    return await RegistrarRechazoDelPasoAsync(sesion, operacion, error, cancellationToken);
                }
                catch (OperacionIdentidadException)
                {
                    return Resultado.Fallido(ErrorUsuarios.AdministracionPendiente);
                }

                var actualizada = await identidades.ObtenerAsync(sujeto, cancellationToken);
                if (actualizada is null || !await PasoConfirmadoAsync(actualizada, datos, paso, cancellationToken))
                {
                    return Resultado.Fallido(ErrorUsuarios.AdministracionPendiente);
                }
            }

            operacion = operacion with { Restantes = operacion.Restantes & ~paso, Paso = CambiosUsuario.Ninguno };
            await sesion.GuardarPasoAsync(operacion, paso == CambiosUsuario.Correo, cancellationToken);
        }

        await sesion.CerrarOperacionAsync(operacion with { Estado = EstadoOperacion.Exitosa }, reloj.GetUtcNow(),
            operacion.Cambios != CambiosUsuario.Ninguno, null, cancellationToken);
        return Resultado.Exitoso();
    }

    private async Task<Resultado> RegistrarRechazoDelPasoAsync(ISesionAdministracionUsuarios sesion,
        OperacionAdministrativa operacion, OperacionIdentidadException error, CancellationToken cancellationToken)
    {
        if (operacion.Restantes == operacion.Cambios)
        {
            var detalle = operacion.Paso == CambiosUsuario.Correo && error.Error == ErrorIdentidad.Conflicto
                ? ErrorUsuarios.CorreoDuplicado
                : operacion.Paso == CambiosUsuario.Correo && error.EstadoRemoto == 400
                    ? ErrorUsuarios.PerfilRechazado : ErrorUsuarios.AdministracionPendiente;
            if (detalle != ErrorUsuarios.AdministracionPendiente)
            {
                await sesion.CerrarOperacionAsync(operacion with { Estado = EstadoOperacion.Fallida, Error = detalle },
                    reloj.GetUtcNow(), false, null, cancellationToken);
                return Resultado.Fallido(detalle);
            }
        }

        await sesion.GuardarPasoAsync(operacion with { Paso = CambiosUsuario.Ninguno }, false, cancellationToken);
        return Resultado.Fallido(ErrorUsuarios.AdministracionPendiente);
    }

    private Task AplicarPasoAsync(CuentaKeycloak cuenta, EdicionUsuario datos, CambiosUsuario paso,
        CancellationToken cancellationToken) => paso switch
    {
        CambiosUsuario.Correo => identidades.ActualizarDatosAsync(cuenta.Id, new DatosCuentaKeycloak(
            NuevoCorreo: datos.Email,
            AccionesRequeridas: cuenta.AccionesRequeridas.Append(nameof(AccionInvitacion.VERIFY_EMAIL)).Distinct().ToArray()),
            cancellationToken),
        CambiosUsuario.AgregarRol => identidades.AsignarRolAsync(cuenta.Id, datos.Rol!, cancellationToken),
        CambiosUsuario.RetirarRol => identidades.RetirarRolAsync(cuenta.Id, RolOpuesto(datos.Rol!), cancellationToken),
        CambiosUsuario.Habilitacion => identidades.CambiarHabilitacionAsync(cuenta.Id, datos.Activo!.Value, cancellationToken),
        CambiosUsuario.Sesiones => identidades.CerrarSesionesAsync(cuenta.Id, cancellationToken),
        _ => throw new InvalidOperationException("Paso de edición inválido.")
    };

    private async Task<bool> PasoConfirmadoAsync(CuentaKeycloak cuenta, EdicionUsuario datos, CambiosUsuario paso,
        CancellationToken cancellationToken) => paso switch
    {
        CambiosUsuario.Correo => CorreoCoincide(cuenta, datos.Email!)
            && (cuenta.CorreoVerificado || cuenta.AccionesRequeridas.Contains(nameof(AccionInvitacion.VERIFY_EMAIL))),
        CambiosUsuario.AgregarRol => (await identidades.ObtenerRolesAsync(cuenta.Id, cancellationToken)).Contains(datos.Rol),
        CambiosUsuario.RetirarRol => !(await identidades.ObtenerRolesAsync(cuenta.Id, cancellationToken)).Contains(RolOpuesto(datos.Rol!)),
        CambiosUsuario.Habilitacion => cuenta.Habilitada == datos.Activo,
        CambiosUsuario.Sesiones => !await identidades.TieneSesionesAsync(cuenta.Id, cancellationToken),
        _ => false
    };

    private static bool CorreoCoincide(CuentaKeycloak cuenta, string correo) =>
        string.Equals(cuenta.Correo, correo, StringComparison.OrdinalIgnoreCase);

    private static string RolOpuesto(string rol) => rol == Roles.Administrador ? Roles.Recepcionista : Roles.Administrador;
}
