using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using AutoTallerPro.Mecanicos.Domain;
namespace AutoTallerPro.Mecanicos.Infrastructure.Persistencia;
[BsonIgnoreExtraElements]
internal sealed class MecanicoDocumento
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; } = "";
    [BsonElement("nombres")] public string Nombres { get; set; } = "";
    [BsonElement("apellidos")] public string Apellidos { get; set; } = "";
    [BsonElement("documento")] public string Documento { get; set; } = "";
    [BsonElement("telefono")] public string? Telefono { get; set; }
    [BsonElement("correo")] public string? Correo { get; set; }
    [BsonElement("especialidad")] public string Especialidad { get; set; } = "";
    [BsonElement("activo")] public bool Activo { get; set; }
    [BsonElement("fechaRegistro")] public DateTime FechaRegistro { get; set; }
    [BsonElement("fechaActualizacion")] public DateTime FechaActualizacion { get; set; }
    [BsonElement("versionEsquema")] public int VersionEsquema { get; set; } = 1;
    [BsonElement("usuarioRegistro")] public string? UsuarioRegistro { get; set; }
    [BsonElement("usuarioActualizacion")] public string? UsuarioActualizacion { get; set; }
    internal Mecanico Entidad() => Mecanico.Restaurar(Id, Nombres, Apellidos, Documento, Telefono, Correo, Especialidad, Activo,
        new DateTimeOffset(DateTime.SpecifyKind(FechaRegistro, DateTimeKind.Utc)), new DateTimeOffset(DateTime.SpecifyKind(FechaActualizacion, DateTimeKind.Utc)), UsuarioRegistro, UsuarioActualizacion);
    internal static MecanicoDocumento Desde(Mecanico entidad) => new()
    {
        Id = entidad.Id, Nombres = entidad.Nombres, Apellidos = entidad.Apellidos, Documento = entidad.Documento,
        Telefono = entidad.Telefono, Correo = entidad.Correo, Especialidad = entidad.Especialidad, Activo = entidad.Activo,
        FechaRegistro = entidad.FechaRegistro.UtcDateTime, FechaActualizacion = entidad.FechaActualizacion.UtcDateTime,
        UsuarioRegistro = entidad.UsuarioRegistro, UsuarioActualizacion = entidad.UsuarioActualizacion
    };
}
