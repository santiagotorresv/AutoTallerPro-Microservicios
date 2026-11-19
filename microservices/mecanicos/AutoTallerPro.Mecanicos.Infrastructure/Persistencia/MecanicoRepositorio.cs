using MongoDB.Driver;
using AutoTallerPro.Mecanicos.Domain;
using AutoTallerPro.Mecanicos.Domain.Resultados;
using AutoTallerPro.Mecanicos.Application.Puertos;
namespace AutoTallerPro.Mecanicos.Infrastructure.Persistencia;
public sealed class MecanicoRepositorio(IMongoDatabase baseDatos) : IMecanicoRepositorio
{
    private readonly IMongoCollection<MecanicoDocumento> coleccion = baseDatos.GetCollection<MecanicoDocumento>("mecanicos");
    public async Task<IReadOnlyList<Mecanico>> ListarAsync(CancellationToken ct)
        => (await coleccion.Find(Builders<MecanicoDocumento>.Filter.Empty).ToListAsync(ct)).Select(x => x.Entidad()).ToArray();
    public async Task<Mecanico?> ObtenerAsync(string id, CancellationToken ct)
        => (await coleccion.Find(x => x.Id == id).FirstOrDefaultAsync(ct))?.Entidad();
    public async Task<bool> ExisteIdentificadorAsync(string identificador, string exceptoId, CancellationToken ct)
        => await coleccion.Find(x => x.Documento == identificador && x.Id != exceptoId).AnyAsync(ct);
    public async Task<Resultado<Mecanico>> GuardarAsync(Mecanico entidad, bool nuevo, CancellationToken ct)
    {
        try
        {
            if (nuevo) await coleccion.InsertOneAsync(MecanicoDocumento.Desde(entidad), cancellationToken: ct);
            else
            {
                var resultado = await coleccion.ReplaceOneAsync(x => x.Id == entidad.Id, MecanicoDocumento.Desde(entidad), cancellationToken: ct);
                if (resultado.MatchedCount == 0) return Resultado<Mecanico>.Fallo("mecanico.ausente", "No se encontró el mecánico.", TipoError.NoEncontrado);
            }
            return Resultado<Mecanico>.Exito(entidad);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { return Resultado<Mecanico>.Fallo("mecanico.duplicado", "Ya existe un mecánico con ese documento.", TipoError.Conflicto); }
    }
    public async Task<Resultado<bool>> EliminarAsync(string id, CancellationToken ct)
    {
        var resultado = await coleccion.DeleteOneAsync(x => x.Id == id, ct);
        return resultado.DeletedCount == 1 ? Resultado<bool>.Exito(true) : Resultado<bool>.Fallo("mecanico.ausente", "No se encontró el mecánico.", TipoError.NoEncontrado);
    }
}
