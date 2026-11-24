using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
namespace AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
public sealed class ClienteServicio(IClienteRepositorio repositorio)
{
    public async Task<Resultado<IReadOnlyList<Cliente>>> ListarAsync(string? buscar, string? ordenar, bool descendente, CancellationToken ct)
    {
        var registros = await repositorio.ListarAsync(ct);
        var filtrados = registros.Where(x => string.IsNullOrWhiteSpace(buscar) ||
            x.Documento.Contains(buscar.Trim(), StringComparison.OrdinalIgnoreCase) || x.Nombre.Contains(buscar!.Trim(), StringComparison.OrdinalIgnoreCase));
        Func<Cliente, IComparable> clave = ordenar?.ToLowerInvariant() switch
        {
            "fecha" => x => x.FechaRegistro,
            "nombre" => x => x.Nombre,
            "telefono" => x => x.Telefono ?? "",
            "correo" => x => x.Correo ?? "",
            "documento" => x => x.Documento,
            _ => x => x.Nombre
        };
        var ordenados = descendente ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        return Resultado<IReadOnlyList<Cliente>>.Exito(ordenados.ThenBy(x => x.Id).ToArray());
    }
    public async Task<Resultado<Cliente>> ObtenerAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return Resultado<Cliente>.Fallo("cliente.id", "El identificador no es válido.");
        var entidad = await repositorio.ObtenerAsync(id, ct);
        return entidad is null ? Resultado<Cliente>.Fallo("cliente.ausente", "No se encontró el registro.", TipoError.NoEncontrado) : Resultado<Cliente>.Exito(entidad);
    }
    // usuario: quien realiza la operación (bitácora); el registro conserva a quien lo creó.
    public async Task<Resultado<Cliente>> GuardarAsync(Guid? id, DatosCliente datos, CancellationToken ct, string? usuario = null)
    {
        Cliente? anterior = null;
        if (id.HasValue)
        {
            var consulta = await ObtenerAsync(id.Value, ct);
            if (!consulta.EsExitoso) return consulta;
            anterior = consulta.Valor;
        }
        var resultado = Cliente.Crear(id ?? Guid.NewGuid(), datos.Documento, datos.Nombre, datos.Telefono, datos.Correo, datos.Activo, anterior?.FechaRegistro,
            usuarioRegistro: anterior is null ? usuario : anterior.UsuarioRegistro, usuarioActualizacion: usuario);
        if (!resultado.EsExitoso) return resultado;
        if (await repositorio.ExisteIdentificadorAsync(resultado.Valor.Documento, resultado.Valor.Id, ct))
            return Resultado<Cliente>.Fallo("cliente.duplicado", "Ya existe un registro con ese identificador de negocio.", TipoError.Conflicto);
        return await repositorio.GuardarAsync(resultado.Valor, !id.HasValue, ct);
    }
    public async Task<Resultado<bool>> EliminarAsync(Guid id, CancellationToken ct)
    {
        var consulta = await ObtenerAsync(id, ct);
        if (!consulta.EsExitoso) return Resultado<bool>.Fallo(consulta.Error!);
        return await repositorio.EliminarAsync(id, ct);
    }
}
