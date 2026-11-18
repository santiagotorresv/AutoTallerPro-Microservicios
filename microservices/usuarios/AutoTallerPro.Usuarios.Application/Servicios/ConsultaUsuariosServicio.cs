using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Persistencia;
using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public sealed record UsuarioAdministrado(
    int Id, string Username, string? Email, string? Nombre, string? Apellido,
    IReadOnlyList<string> Roles, bool Activo, string EstadoInvitacion);

public sealed record PaginaUsuarios(IReadOnlyList<UsuarioAdministrado> Usuarios, int Total, int Pagina, int TamanoPagina);

public sealed record ResumenUsuarios(int Total, int Activos, int AdministradoresActivos);

public sealed class ConsultaUsuariosServicio(IConsultaFichasUsuarios fichas, IIdentidadesKeycloak identidades)
{
    public async Task<PaginaUsuarios> ListarAsync(
        string emisor, string? busqueda, string? rol, bool? activo, int pagina, int tamanoPagina,
        CancellationToken cancellationToken)
    {
        busqueda = busqueda?.Trim();
        if (busqueda is { Length: > 254 } || (rol is not null && !Roles.EsDeNegocio(rol))
            || pagina < 1 || tamanoPagina is < 1 or > 100)
        {
            throw new OperacionUsuariosException(ErrorUsuarios.DatosInvalidos);
        }

        var usuarios = await ConsultarAsync(emisor, busqueda, rol, activo, cancellationToken);
        long omitir = ((long)pagina - 1) * tamanoPagina;
        var resultados = omitir < usuarios.Length ? usuarios.Skip((int)omitir).Take(tamanoPagina).ToArray() : [];
        return new PaginaUsuarios(resultados, usuarios.Length, pagina, tamanoPagina);
    }

    public async Task<UsuarioAdministrado?> DetalleAsync(string emisor, int id, CancellationToken cancellationToken)
    {
        var ficha = await fichas.ObtenerAsync(emisor, id, cancellationToken);
        return ficha is null ? null : await ObtenerUsuarioAsync(ficha, null, null, null, cancellationToken);
    }

    public async Task<ResumenUsuarios> ResumenAsync(string emisor, CancellationToken cancellationToken)
    {
        var usuarios = await ConsultarAsync(emisor, null, null, null, cancellationToken);
        return new ResumenUsuarios(usuarios.Length, usuarios.Count(usuario => usuario.Activo),
            usuarios.Count(usuario => usuario.Activo && usuario.Roles.Contains(Roles.Administrador)));
    }

    private async Task<UsuarioAdministrado[]> ConsultarAsync(
        string emisor, string? busqueda, string? rol, bool? activo, CancellationToken cancellationToken)
    {
        var listado = await fichas.ListarAsync(emisor, cancellationToken);
        var usuarios = new UsuarioAdministrado?[listado.Count];
        var concurrencia = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(Enumerable.Range(0, listado.Count), concurrencia, async (i, token) =>
        {
            usuarios[i] = await ObtenerUsuarioAsync(listado[i], busqueda, rol, activo, token);
        });

        return usuarios.OfType<UsuarioAdministrado>().OrderBy(usuario => usuario.Id).ToArray();
    }

    private async Task<UsuarioAdministrado?> ObtenerUsuarioAsync(
        FichaUsuario ficha, string? busqueda, string? rol, bool? activo, CancellationToken cancellationToken)
    {
        try
        {
            var cuenta = await identidades.ObtenerAsync(ficha.Sujeto, cancellationToken);
            if (cuenta is null || (activo is not null && activo != cuenta.Habilitada)
                || (!string.IsNullOrEmpty(busqueda) && !cuenta.Username.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                    && cuenta.Correo?.Contains(busqueda, StringComparison.OrdinalIgnoreCase) != true))
            {
                return null;
            }

            var roles = await identidades.ObtenerRolesAsync(ficha.Sujeto, cancellationToken);
            return rol is null || roles.Contains(rol)
                ? new UsuarioAdministrado(ficha.Id, cuenta.Username, cuenta.Correo, cuenta.Nombre, cuenta.Apellido,
                    roles, cuenta.Habilitada, EstadoInvitacion.DesdeCuenta(cuenta, ficha.Estado))
                : null;
        }
        catch (OperacionIdentidadException error) when (error.Error == ErrorIdentidad.NoEncontrado)
        {
            return null;
        }
    }
}

internal static class EstadoInvitacion
{
    internal static string DesdeRegistro(EstadoAlta? estado) => estado switch
    {
        EstadoAlta.InvitacionPendiente => "pendiente",
        EstadoAlta.EnvioIncierto => "envioIncierto",
        EstadoAlta.CorreoSolicitado => "correoSolicitado",
        _ => "sinRegistro"
    };

    internal static string DesdeCuenta(CuentaKeycloak cuenta, EstadoAlta? estado) =>
        estado is not null && cuenta.CorreoVerificado
        && !cuenta.AccionesRequeridas.Any(accion => accion is "VERIFY_EMAIL" or "UPDATE_PASSWORD")
            ? "completada"
            : DesdeRegistro(estado);
}

