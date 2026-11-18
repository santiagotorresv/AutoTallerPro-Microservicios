using System.Security.Cryptography;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Validaciones;
using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public sealed record SolicitudAltaUsuario(string? Email, string? Rol, string? Nombre, string? Apellido);
public sealed record UsuarioCreado(int Id, string Username, string EstadoInvitacion);

public sealed class AltaUsuariosServicio(IIdentidadesKeycloak identidades, IAltasUsuarios altas, TimeProvider reloj)
{
    public async Task<UsuarioCreado> CrearAsync(
        IdentidadKeycloak actor, Guid clave, SolicitudAltaUsuario entrada, CancellationToken cancellationToken)
    {
        await ValidarAdministradorAsync(actor, cancellationToken);
        var datos = Validar(entrada, clave);
        byte[] huella = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(datos));
        await using var operacion = await altas.AbrirAsync(actor, clave, datos.Email, huella, cancellationToken);

        if (operacion.Alta.Estado is EstadoAlta.Conflicto or EstadoAlta.PerfilRechazado)
        {
            throw new OperacionUsuariosException(operacion.Alta.Estado == EstadoAlta.Conflicto
                ? ErrorUsuarios.CorreoDuplicado : ErrorUsuarios.PerfilRechazado);
        }

        if (operacion.Alta.FichaId is null)
        {
            string sujeto = await ObtenerOSolicitarIdentidadAsync(operacion, datos, cancellationToken);
            var cuenta = await identidades.ObtenerAsync(sujeto, cancellationToken);
            if (cuenta?.CorrelacionAlta != operacion.Alta.Id || cuenta.Username != operacion.Alta.Username
                || !string.Equals(cuenta.Correo, datos.Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new OperacionUsuariosException(ErrorUsuarios.ReconciliacionPendiente);
            }

            var roles = await identidades.ObtenerRolesAsync(sujeto, cancellationToken);
            if (roles.Count == 0)
            {
                await identidades.AsignarRolAsync(sujeto, datos.Rol, cancellationToken);
                roles = await identidades.ObtenerRolesAsync(sujeto, cancellationToken);
            }

            if (roles.Count != 1 || roles[0] != datos.Rol)
            {
                throw new OperacionUsuariosException(ErrorUsuarios.ReconciliacionPendiente);
            }

            await operacion.VincularFichaAsync(reloj.GetUtcNow(), cancellationToken);
        }

        var estado = operacion.Alta.Estado;
        if (estado == EstadoAlta.InvitacionPendiente)
        {
            estado = await SolicitarCorreoAsync(operacion, cancellationToken);
        }

        return new UsuarioCreado(operacion.Alta.FichaId!.Value, operacion.Alta.Username!,
            EstadoInvitacion.DesdeRegistro(estado));
    }

    private static DatosAlta Validar(SolicitudAltaUsuario solicitud, Guid clave)
    {
        string? email = solicitud.Email?.Trim().ToLowerInvariant();
        string? nombre = solicitud.Nombre?.Trim();
        string? apellido = solicitud.Apellido?.Trim();
        if (clave == Guid.Empty || !ReglasCuenta.EmailValido(email) || !Roles.EsDeNegocio(solicitud.Rol)
            || nombre is not { Length: >= 1 and <= 100 } || apellido is not { Length: >= 1 and <= 100 })
        {
            throw new OperacionUsuariosException(ErrorUsuarios.DatosInvalidos);
        }

        return new DatosAlta(email!, solicitud.Rol!, nombre, apellido);
    }

    private async Task ValidarAdministradorAsync(IdentidadKeycloak actor, CancellationToken cancellationToken)
    {
        if (Guid.TryParseExact(actor.Sujeto, "D", out _))
        {
            try
            {
                var cuenta = await identidades.ObtenerAsync(actor.Sujeto, cancellationToken);
                if (cuenta?.Habilitada == true
                    && (await identidades.ObtenerRolesAsync(actor.Sujeto, cancellationToken)).Contains(Roles.Administrador))
                {
                    return;
                }
            }
            catch (OperacionIdentidadException error) when (error.Error is ErrorIdentidad.Prohibido or ErrorIdentidad.NoEncontrado)
            {
            }
        }

        throw new OperacionUsuariosException(ErrorUsuarios.AdministradorNoVigente);
    }

    private async Task<string> ObtenerOSolicitarIdentidadAsync(
        IAltaUsuario operacion, DatosAlta datos, CancellationToken cancellationToken)
    {
        if (operacion.Alta.Sujeto is { } sujeto)
        {
            return sujeto;
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
                return creado;
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
                    await RechazarAsync(operacion, EstadoAlta.Conflicto, ErrorUsuarios.CorreoDuplicado, cancellationToken);
                }

                var coincidente = await identidades.BuscarPorUsernameAsync(candidato, cancellationToken);
                if (coincidente?.CorrelacionAlta == operacion.Alta.Id)
                {
                    return await ResolverCuentaPorCorreoAsync(operacion, coincidente, cancellationToken);
                }

                if (coincidente is null && error.CampoEnConflicto != CampoIdentidad.Username)
                {
                    throw new OperacionUsuariosException(ErrorUsuarios.ReconciliacionPendiente);
                }
            }
            catch (OperacionIdentidadException error) when (error.EstadoRemoto == 400)
            {
                await RechazarAsync(operacion, EstadoAlta.PerfilRechazado, ErrorUsuarios.PerfilRechazado, cancellationToken);
            }
        }

        throw new OperacionUsuariosException(ErrorUsuarios.UsernameNoDisponible);
    }

    private static async Task<string> ResolverCuentaPorCorreoAsync(
        IAltaUsuario operacion, CuentaKeycloak cuenta, CancellationToken cancellationToken)
    {
        if (cuenta.CorrelacionAlta == operacion.Alta.Id)
        {
            if (cuenta.Username != operacion.Alta.Username)
            {
                throw new OperacionUsuariosException(ErrorUsuarios.ReconciliacionPendiente);
            }

            await operacion.GuardarSujetoAsync(cuenta.Id, cancellationToken);
            return cuenta.Id;
        }

        await operacion.GuardarEstadoAsync(EstadoAlta.Conflicto, cancellationToken);
        throw new OperacionUsuariosException(ErrorUsuarios.CorreoDuplicado);
    }

    private static async Task RechazarAsync(
        IAltaUsuario operacion, EstadoAlta estado, ErrorUsuarios error, CancellationToken cancellationToken)
    {
        await operacion.GuardarEstadoAsync(estado, cancellationToken);
        throw new OperacionUsuariosException(error);
    }

    private async Task<EstadoAlta> SolicitarCorreoAsync(IAltaUsuario operacion, CancellationToken cancellationToken)
    {
        try
        {
            await operacion.GuardarEstadoAsync(EstadoAlta.EnvioIncierto, cancellationToken);
            try
            {
                await identidades.SolicitarInvitacionAsync(operacion.Alta.Sujeto!, cancellationToken);
            }
            catch (OperacionIdentidadException error)
            {
                if (error.ResultadoIncierto)
                {
                    return EstadoAlta.EnvioIncierto;
                }

                await operacion.GuardarEstadoAsync(EstadoAlta.InvitacionPendiente, cancellationToken);
                return EstadoAlta.InvitacionPendiente;
            }

            await operacion.GuardarEstadoAsync(EstadoAlta.CorreoSolicitado, cancellationToken);
            return EstadoAlta.CorreoSolicitado;
        }
        catch (PersistenciaEstadoAltaException)
        {
            return EstadoAlta.EnvioIncierto;
        }
    }

    private sealed record DatosAlta(string Email, string Rol, string Nombre, string Apellido);
}

