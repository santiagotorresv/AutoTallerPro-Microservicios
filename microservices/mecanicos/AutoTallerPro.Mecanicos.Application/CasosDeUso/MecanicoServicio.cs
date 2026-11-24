using System.Security.Cryptography;
using AutoTallerPro.Mecanicos.Domain;
using AutoTallerPro.Mecanicos.Domain.Resultados;
using AutoTallerPro.Mecanicos.Application.Puertos;
namespace AutoTallerPro.Mecanicos.Application.CasosDeUso;
public sealed class MecanicoServicio(IMecanicoRepositorio repositorio)
{
    public async Task<Resultado<IReadOnlyList<Mecanico>>> ListarAsync(string? buscar, string? ordenar, bool descendente, CancellationToken ct)
    {
        var registros = await repositorio.ListarAsync(ct);
        var filtrados = registros.Where(x => string.IsNullOrWhiteSpace(buscar) ||
            $"{x.Nombres} {x.Apellidos} {x.Documento} {x.Especialidad}".Contains(buscar.Trim(), StringComparison.OrdinalIgnoreCase));
        Func<Mecanico, IComparable> clave = ordenar?.ToLowerInvariant() switch
        {
            "documento" => x => x.Documento, "especialidad" => x => x.Especialidad,
            "fecha" => x => x.FechaRegistro, "nombres" => x => x.Nombres,
            _ => x => x.Apellidos
        };
        var ordenados = descendente ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        return Resultado<IReadOnlyList<Mecanico>>.Exito(ordenados.ThenBy(x => x.Id).ToArray());
    }
    public async Task<Resultado<Mecanico>> ObtenerAsync(string id, CancellationToken ct)
    {
        if (!Mecanico.IdValido(id)) return Resultado<Mecanico>.Fallo("mecanico.id", "El identificador del mecánico debe tener 24 caracteres hexadecimales.");
        var entidad = await repositorio.ObtenerAsync(id.ToLowerInvariant(), ct);
        return entidad is null ? Resultado<Mecanico>.Fallo("mecanico.ausente", "No se encontró el mecánico.", TipoError.NoEncontrado) : Resultado<Mecanico>.Exito(entidad);
    }
    public async Task<Resultado<Mecanico>> GuardarAsync(string? id, DatosMecanico datos, CancellationToken ct, string? usuario = null)
    {
        Mecanico? anterior = null;
        if (id is not null)
        {
            var consulta = await ObtenerAsync(id, ct);
            if (!consulta.EsExitoso) return consulta;
            anterior = consulta.Valor;
        }
        var resultado = Mecanico.Crear(id ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(),
            datos.Nombres, datos.Apellidos, datos.Documento, datos.Telefono, datos.Correo, datos.Especialidad, datos.Activo, anterior?.FechaRegistro, usuarioRegistro: anterior is null ? usuario : anterior.UsuarioRegistro, usuarioActualizacion: usuario);
        if (!resultado.EsExitoso) return resultado;
        if (await repositorio.ExisteIdentificadorAsync(resultado.Valor.Documento, resultado.Valor.Id, ct))
            return Resultado<Mecanico>.Fallo("mecanico.duplicado", "Ya existe un mecánico con ese documento.", TipoError.Conflicto);
        return await repositorio.GuardarAsync(resultado.Valor, id is null, ct);
    }
    public async Task<Resultado<bool>> EliminarAsync(string id, CancellationToken ct)
    {
        var consulta = await ObtenerAsync(id, ct);
        return consulta.EsExitoso ? await repositorio.EliminarAsync(consulta.Valor.Id, ct) : Resultado<bool>.Fallo(consulta.Error!);
    }
}
