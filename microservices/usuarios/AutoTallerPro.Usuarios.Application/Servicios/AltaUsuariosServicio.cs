using System.Security.Cryptography;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Validaciones;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using AutoTallerPro.Usuarios.Domain.Validaciones;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public sealed record SolicitudAltaUsuario(string? Email, string? Rol, string? Nombre, string? Apellido);
public sealed record UsuarioCreado(int Id, string Username, string EstadoInvitacion);

public sealed class AltaUsuariosServicio(IIdentidadesKeycloak identidades, IAltasUsuarios altas, TimeProvider reloj,
    IAdministracionUsuarios administracion)
{
    public const string ActorAprovisionamiento = "aprovisionamiento-p4";

    public async Task<Resultado<UsuarioCreado>> CrearAsync(
        IdentidadKeycloak actor, Guid clave, SolicitudAltaUsuario entrada, CancellationToken cancellationToken)
    {
        var autorizacion = await ValidacionAdministrador.ValidarAsync(identidades, actor, cancellationToken);
        if (!autorizacion.EsExitoso)
        {
            return Resultado<UsuarioCreado>.Fallido(autorizacion.Error);
        }

        var datos = Validar(entrada, clave);
        return datos.EsExitoso
            ? await CrearAsync(actor, clave, datos.Valor, null, cancellationToken)
            : Resultado<UsuarioCreado>.Fallido(datos.Error!);
    }

    public async Task<Resultado<UsuarioCreado>> AprovisionarPrimeroAsync(
        string emisor, Guid clave, string? email, string? nombre, string? apellido,
        IConsultaFichasUsuarios fichas, CancellationToken cancellationToken)
    {
        var datos = Validar(new SolicitudAltaUsuario(email, Roles.Administrador, nombre, apellido), clave);
        if (!datos.EsExitoso)
        {
            return Resultado<UsuarioCreado>.Fallido(datos.Error);
        }

        if (!string.Equals(emisor, await identidades.ObtenerEmisorAsync(cancellationToken), StringComparison.Ordinal))
        {
            return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.EmisorNoCoincide);
        }
        var actor = new IdentidadKeycloak(emisor, ActorAprovisionamiento);
        var apertura = await administracion.AbrirAsync(emisor, cancellationToken);
        if (!apertura.EsExitoso)
        {
            return Resultado<UsuarioCreado>.Fallido(apertura.Error);
        }
        await using var control = apertura.Valor;
        var busqueda = await altas.BuscarAprovisionamientoAsync(actor, cancellationToken);
        if (!busqueda.EsExitoso)
        {
            return Resultado<UsuarioCreado>.Fallido(busqueda.Error);
        }
        var previa = busqueda.Valor;
        if (previa is not null && previa.Clave != clave)
        {
            return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.PrimerAdministradorNoDisponible);
        }

        var gestionadas = await identidades.ListarAdministradasAsync(cancellationToken);
        if (previa?.Alta.FichaId is null)
        {
            if ((await fichas.ListarAsync(emisor, cancellationToken)).Count != 0
                || gestionadas.Any(cuenta => !EsIdentidadDelAlta(cuenta, previa?.Alta)))
            {
                return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.PrimerAdministradorNoDisponible);
            }
        }
        else
        {
            var vinculada = gestionadas.SingleOrDefault(cuenta => EsIdentidadDelAlta(cuenta, previa.Alta));
            if (vinculada is null || !(await identidades.ObtenerRolesAsync(vinculada.Id, cancellationToken))
                .Contains(Roles.Administrador))
            {
                return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.PrimerAdministradorNoDisponible);
            }
        }

        return await CrearAsync(actor, clave, datos.Valor, control, cancellationToken);
    }

    private static bool EsIdentidadDelAlta(CuentaKeycloak cuenta, AltaUsuario? alta) => alta is not null
        && cuenta.CorrelacionAlta == alta.Id && cuenta.Username == alta.Username
        && (alta.Sujeto is null || cuenta.Id == alta.Sujeto);

    private async Task<Resultado<UsuarioCreado>> CrearAsync(IdentidadKeycloak actor, Guid clave, DatosAlta datos,
        ISesionAdministracionUsuarios? control, CancellationToken cancellationToken)
    {
        byte[] huella = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(datos));
        var apertura = await altas.AbrirAsync(actor, clave, datos.Email, huella, cancellationToken);
        if (!apertura.EsExitoso)
        {
            return Resultado<UsuarioCreado>.Fallido(apertura.Error);
        }
        await using var operacion = apertura.Valor;

        if (operacion.Alta.Estado is EstadoAlta.Conflicto or EstadoAlta.PerfilRechazado)
        {
            return Resultado<UsuarioCreado>.Fallido(operacion.Alta.Estado == EstadoAlta.Conflicto
                ? ErrorUsuarios.CorreoDuplicado : ErrorUsuarios.PerfilRechazado);
        }

        if (operacion.Alta.FichaId is null)
        {
            var identidad = await ObtenerOSolicitarIdentidadAsync(operacion, datos, cancellationToken);
            if (!identidad.EsExitoso)
            {
                return Resultado<UsuarioCreado>.Fallido(identidad.Error);
            }
            string sujeto = identidad.Valor;
            var cuenta = await identidades.ObtenerAsync(sujeto, cancellationToken);
            if (cuenta?.CorrelacionAlta != operacion.Alta.Id || cuenta.Username != operacion.Alta.Username
                || !string.Equals(cuenta.Correo, datos.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.ReconciliacionPendiente);
            }

            var roles = await identidades.ObtenerRolesAsync(sujeto, cancellationToken);
            if (roles.Count == 0)
            {
                await identidades.AsignarRolAsync(sujeto, datos.Rol, cancellationToken);
                roles = await identidades.ObtenerRolesAsync(sujeto, cancellationToken);
            }

            if (roles.Count != 1 || roles[0] != datos.Rol)
            {
                return Resultado<UsuarioCreado>.Fallido(ErrorUsuarios.ReconciliacionPendiente);
            }

            await operacion.VincularFichaAsync(reloj.GetUtcNow(), cancellationToken);
        }

        var estado = operacion.Alta.Estado;
        if (estado == EstadoAlta.InvitacionPendiente)
        {
            estado = await SolicitarCorreoAsync(actor.Emisor, operacion, datos.Email, control, cancellationToken);
        }

        return Resultado<UsuarioCreado>.Exitoso(new UsuarioCreado(operacion.Alta.FichaId!.Value,
            operacion.Alta.Username!, EstadoInvitacion.DesdeRegistro(estado)));
    }

    private static Resultado<DatosAlta> Validar(SolicitudAltaUsuario solicitud, Guid clave)
    {
        string? email = solicitud.Email?.Trim().ToLowerInvariant();
        string? nombre = solicitud.Nombre?.Trim();
        string? apellido = solicitud.Apellido?.Trim();
        return Validacion.Requerir(clave != Guid.Empty && ReglasCuenta.EmailValido(email) && Roles.EsDeNegocio(solicitud.Rol)
            && nombre is { Length: >= 1 and <= 100 } && apellido is { Length: >= 1 and <= 100 },
            ErrorUsuarios.DatosInvalidos, () => new DatosAlta(email!, solicitud.Rol!, nombre!, apellido!));
    }

    private async Task<Resultado<string>> ObtenerOSolicitarIdentidadAsync(
        IAltaUsuario operacion, DatosAlta datos, CancellationToken cancellationToken)
    {
        if (operacion.Alta.Sujeto is { } sujeto)
        {
            return Resultado<string>.Exitoso(sujeto);
        }

        var existente = await identidades.BuscarPorCorreoAsync(datos.Email, cancellationToken);
        if (existente is not null)
        {
            return await ResolverCuentaPorCorreoAsync(operacion, existente, cancellationToken);
        }

        var candidatos = GeneradorNombreUsuarioService.GenerarCandidatos(datos.Email);
        if (operacion.Alta.Username is { } anterior)
        {
            candidatos = candidatos.SkipWhile(candidato => candidato != anterior);
        }

        foreach (string candidato in candidatos)
        {
            var cuenta = await identidades.BuscarPorUsernameAsync(candidato, cancellationToken);
            if (cuenta is not null)
            {
                if (cuenta.CorrelacionAlta == operacion.Alta.Id)
                {
                    return await ResolverCuentaPorCorreoAsync(operacion, cuenta, cancellationToken);
                }

                continue;
            }

            if (operacion.Alta.Username != candidato)
            {
                await operacion.GuardarCandidatoAsync(candidato, cancellationToken);
            }

            try
            {
                string creado = await identidades.CrearAsync(
                    new NuevaCuentaKeycloak(candidato, datos.Email, datos.Nombre, datos.Apellido, operacion.Alta.Id),
                    cancellationToken);
                await operacion.GuardarSujetoAsync(creado, cancellationToken);
                return Resultado<string>.Exitoso(creado);
            }
            catch (OperacionIdentidadException error) when (error.Error == ErrorIdentidad.Conflicto)
            {
                existente = await identidades.BuscarPorCorreoAsync(datos.Email, cancellationToken);
                if (existente is not null)
                {
                    return await ResolverCuentaPorCorreoAsync(operacion, existente, cancellationToken);
                }

                if (error.CampoEnConflicto == CampoIdentidad.Correo)
                {
                    return await RechazarAsync(operacion, EstadoAlta.Conflicto, ErrorUsuarios.CorreoDuplicado, cancellationToken);
                }

                var coincidente = await identidades.BuscarPorUsernameAsync(candidato, cancellationToken);
                if (coincidente?.CorrelacionAlta == operacion.Alta.Id)
                {
                    return await ResolverCuentaPorCorreoAsync(operacion, coincidente, cancellationToken);
                }

                if (coincidente is null && error.CampoEnConflicto != CampoIdentidad.Username)
                {
                    return Resultado<string>.Fallido(ErrorUsuarios.ReconciliacionPendiente);
                }
            }
            catch (OperacionIdentidadException error) when (error.EstadoRemoto == 400)
            {
                return await RechazarAsync(operacion, EstadoAlta.PerfilRechazado, ErrorUsuarios.PerfilRechazado, cancellationToken);
            }
        }

        return Resultado<string>.Fallido(ErrorUsuarios.UsernameNoDisponible);
    }

    private static async Task<Resultado<string>> ResolverCuentaPorCorreoAsync(
        IAltaUsuario operacion, CuentaKeycloak cuenta, CancellationToken cancellationToken)
    {
        if (cuenta.CorrelacionAlta == operacion.Alta.Id)
        {
            if (cuenta.Username != operacion.Alta.Username)
            {
                return Resultado<string>.Fallido(ErrorUsuarios.ReconciliacionPendiente);
            }

            await operacion.GuardarSujetoAsync(cuenta.Id, cancellationToken);
            return Resultado<string>.Exitoso(cuenta.Id);
        }

        return await RechazarAsync(operacion, EstadoAlta.Conflicto, ErrorUsuarios.CorreoDuplicado, cancellationToken);
    }

    private static async Task<Resultado<string>> RechazarAsync(
        IAltaUsuario operacion, EstadoAlta estado, ErrorUsuarios error, CancellationToken cancellationToken)
    {
        await operacion.GuardarEstadoAsync(estado, cancellationToken);
        return Resultado<string>.Fallido(error);
    }

    private async Task<EstadoAlta> SolicitarCorreoAsync(string emisor, IAltaUsuario operacion, string correo,
        ISesionAdministracionUsuarios? control, CancellationToken cancellationToken)
    {
        try
        {
            if (control is not null)
            {
                return await EnviarSiPendienteAsync(control, operacion, correo, cancellationToken);
            }

            var apertura = await administracion.AbrirAsync(emisor, cancellationToken);
            if (!apertura.EsExitoso)
            {
                return EstadoAlta.InvitacionPendiente;
            }

            await using var sesion = apertura.Valor;
            return await EnviarSiPendienteAsync(sesion, operacion, correo, cancellationToken);
        }
        catch (PersistenciaEstadoAltaException)
        {
            return EstadoAlta.EnvioIncierto;
        }
        catch (OperacionIdentidadException)
        {
            return EstadoAlta.InvitacionPendiente;
        }
    }

    private async Task<EstadoAlta> EnviarSiPendienteAsync(ISesionAdministracionUsuarios sesion, IAltaUsuario operacion,
        string correo, CancellationToken cancellationToken)
    {
        if (await sesion.HayEdicionPendienteAsync(Guid.Empty, cancellationToken))
        {
            return EstadoAlta.InvitacionPendiente;
        }

        int ficha = operacion.Alta.FichaId!.Value;
        Guid id = operacion.Alta.Id;
        var invitacion = await sesion.ObtenerInvitacionAsync(ficha, cancellationToken);
        if (invitacion?.ReservadoPor is { } otra && otra != id)
        {
            return invitacion.Estado;
        }

        var cuenta = await identidades.ObtenerAsync(operacion.Alta.Sujeto!, cancellationToken);
        if (cuenta?.Habilitada != true || !EsIdentidadDelAlta(cuenta, operacion.Alta)
            || !string.Equals(cuenta.Correo, correo, StringComparison.OrdinalIgnoreCase))
        {
            return EstadoAlta.InvitacionPendiente;
        }

        var acciones = Enum.GetValues<AccionInvitacion>()
            .Where(accion => cuenta.AccionesRequeridas.Contains(accion.ToString())).ToArray();
        if (acciones.Length == 0)
        {
            return EstadoAlta.InvitacionPendiente;
        }

        await sesion.ReservarInvitacionAsync(ficha, id, cancellationToken);
        await operacion.GuardarEstadoAsync(EstadoAlta.EnvioIncierto, cancellationToken);
        EstadoAlta estado;
        try
        {
            await identidades.SolicitarInvitacionAsync(cuenta.Id, acciones, cancellationToken);
            estado = EstadoAlta.CorreoSolicitado;
        }
        catch (OperacionIdentidadException error)
        {
            if (error.ResultadoIncierto)
            {
                return EstadoAlta.EnvioIncierto;
            }
            estado = EstadoAlta.InvitacionPendiente;
        }

        await sesion.GuardarInvitacionAsync(ficha, id, estado, cancellationToken);
        await operacion.GuardarEstadoAsync(estado, cancellationToken);
        return estado;
    }

    private sealed record DatosAlta(string Email, string Rol, string Nombre, string Apellido);
}
