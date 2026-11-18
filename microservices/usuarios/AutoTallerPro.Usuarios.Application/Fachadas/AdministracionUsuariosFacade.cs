using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AutoTallerPro.Usuarios.Application.Fabricas;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Application.Validaciones;
using AutoTallerPro.Usuarios.Domain;
using AutoTallerPro.Usuarios.Domain.Resultados;
using AutoTallerPro.Usuarios.Domain.Validaciones;

namespace AutoTallerPro.Usuarios.Application.Fachadas;

public sealed class AdministracionUsuariosFacade(IIdentidadesKeycloak identidades, IFichasAdministrativas fichas,
    IAuditoriaAdministrativa auditoria, IAdministracionUsuarios administracion, TimeProvider reloj)
{
    private const string GrupoAdministrado = "/usuarios-administrados";
    private static readonly CreadorOperacionUsuario CambiosCuenta = new CreadorCambiosCuenta();
    private static readonly CreadorOperacionUsuario ReenvioInvitacion = new CreadorReenvioInvitacion();

    public Task<Resultado> EditarAsync(IdentidadKeycloak actor, int id, Guid clave, EdicionUsuario edicion,
        CancellationToken cancellationToken) =>
        EjecutarAsync(actor, id, clave, TipoAdministracion.Editar, edicion, CambiosCuenta, cancellationToken);

    public Task<Resultado> DesactivarAsync(IdentidadKeycloak actor, int id, Guid clave, CancellationToken cancellationToken) =>
        EjecutarAsync(actor, id, clave, TipoAdministracion.Desactivar, null, CambiosCuenta, cancellationToken);

    public Task<Resultado> ReactivarAsync(IdentidadKeycloak actor, int id, Guid clave, CancellationToken cancellationToken) =>
        EjecutarAsync(actor, id, clave, TipoAdministracion.Reactivar, null, CambiosCuenta, cancellationToken);

    public Task<Resultado> ReenviarInvitacionAsync(IdentidadKeycloak actor, int id, Guid clave, CancellationToken cancellationToken) =>
        EjecutarAsync(actor, id, clave, TipoAdministracion.Reenviar, null, ReenvioInvitacion, cancellationToken);

    private async Task<Resultado> EjecutarAsync(IdentidadKeycloak actor, int id, Guid clave, TipoAdministracion tipo,
        EdicionUsuario? edicion, CreadorOperacionUsuario creador, CancellationToken cancellationToken)
    {
        var apertura = await administracion.AbrirAsync(actor.Emisor, cancellationToken);
        if (!apertura.EsExitoso)
        {
            return Resultado.Fallido(apertura.Error);
        }

        await using var sesion = apertura.Valor;
        OperacionAdministrativa? operacion = null;
        EjecucionOperacionUsuario ejecucion;
        try
        {
            var autorizacion = await ValidacionAdministrador.ValidarAsync(identidades, actor, cancellationToken);
            if (!autorizacion.EsExitoso)
            {
                return await RechazarAsync(autorizacion.Error);
            }

            var datos = Normalizar(tipo, id, clave, edicion);
            if (!datos.EsExitoso)
            {
                return await RechazarAsync(datos.Error);
            }

            byte[] huella = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(datos.Valor));
            operacion = await sesion.BuscarAsync(clave, cancellationToken);
            if (operacion is not null && (operacion.Actor != actor || operacion.FichaId != id || operacion.Tipo != tipo
                || !CryptographicOperations.FixedTimeEquals(operacion.Huella, huella)))
            {
                return await RechazarAsync(new ErrorResultado(ErrorUsuarios.ClaveReutilizada));
            }

            var cuenta = await ObtenerObjetivoAsync(actor.Emisor, id, cancellationToken);
            if (!cuenta.EsExitoso)
            {
                return await RechazarAsync(cuenta.Error);
            }

            if (operacion is { Estado: not EstadoOperacion.Pendiente })
            {
                return operacion.Error is { } error ? Resultado.Fallido(error) : Resultado.Exitoso();
            }

            if (await sesion.HayEdicionPendienteAsync(clave, cancellationToken))
            {
                return await RechazarAsync(new ErrorResultado(ErrorUsuarios.AdministracionPendiente));
            }

            ejecucion = creador.CrearOperacion(new ContextoOperacionUsuario(actor, id, tipo, datos.Valor, cuenta.Valor),
                identidades, reloj);
            var plan = await ejecucion.PrepararAsync(sesion, operacion, cancellationToken);
            if (!plan.EsExitoso)
            {
                return await RechazarAsync(plan.Error);
            }

            if (operacion is null)
            {
                operacion = new OperacionAdministrativa(Guid.NewGuid(), clave, actor, id, tipo, huella,
                    plan.Valor, plan.Valor, CambiosUsuario.Ninguno, EstadoOperacion.Pendiente);
                await sesion.AbrirOperacionAsync(operacion, reloj.GetUtcNow(), cancellationToken);
            }
        }
        catch (OperacionIdentidadException)
        {
            await RegistrarFalloAsync(actor, id, tipo, ErrorUsuarios.AdministracionPendiente, cancellationToken);
            throw;
        }

        return await ejecucion.EjecutarAsync(sesion, operacion, cancellationToken);

        async Task<Resultado> RechazarAsync(ErrorResultado error)
        {
            if (operacion is null or { Estado: EstadoOperacion.Pendiente })
            {
                await RegistrarFalloAsync(actor, id, tipo, error.Codigo, cancellationToken);
            }

            return Resultado.Fallido(error);
        }
    }

    private static Resultado<EdicionUsuario> Normalizar(TipoAdministracion tipo, int id, Guid clave, EdicionUsuario? edicion)
    {
        if (id < 1 || clave == Guid.Empty)
        {
            return Resultado<EdicionUsuario>.Fallido(ErrorUsuarios.DatosInvalidos);
        }

        if (tipo == TipoAdministracion.Editar)
        {
            string? correo = edicion?.Email?.Trim().ToLowerInvariant();
            return Validacion.Requerir(edicion?.Activo is not null && ReglasCuenta.EmailValido(correo)
                && Roles.EsDeNegocio(edicion.Rol), ErrorUsuarios.DatosInvalidos, () => edicion! with { Email = correo });
        }

        return Resultado<EdicionUsuario>.Exitoso(new EdicionUsuario(null, null, tipo switch
        {
            TipoAdministracion.Desactivar => false,
            TipoAdministracion.Reactivar => true,
            _ => null
        }));
    }

    private async Task<Resultado<CuentaKeycloak>> ObtenerObjetivoAsync(string emisor, int id,
        CancellationToken cancellationToken)
    {
        var ficha = await fichas.BuscarPorIdAsync(id, cancellationToken);
        if (ficha?.Identidad.Emisor == emisor && Guid.TryParseExact(ficha.Identidad.Sujeto, "D", out _))
        {
            try
            {
                var cuenta = await identidades.ObtenerAsync(ficha.Identidad.Sujeto, cancellationToken);
                if (cuenta is not null
                    && (await identidades.ObtenerGruposAsync(cuenta.Id, cancellationToken)).Contains(GrupoAdministrado))
                {
                    return Resultado<CuentaKeycloak>.Exitoso(cuenta);
                }
            }
            catch (OperacionIdentidadException error) when (error.Error is ErrorIdentidad.NoEncontrado or ErrorIdentidad.Prohibido)
            {
            }
        }

        return Resultado<CuentaKeycloak>.Fallido(ErrorUsuarios.UsuarioNoEncontrado);
    }

    private Task<long> RegistrarFalloAsync(IdentidadKeycloak actor, int id, TipoAdministracion tipo, ErrorUsuarios error,
        CancellationToken cancellationToken) =>
        auditoria.RegistrarAsync(new RegistroAuditoria(actor, AccionesAdministrativas.Nombre(tipo), "Usuario",
            id.ToString(CultureInfo.InvariantCulture), reloj.GetUtcNow(), ResultadoAuditoria.Fallido,
            AccionesAdministrativas.Codigo(error)), cancellationToken);
}
