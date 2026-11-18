namespace AutoTallerPro.Usuarios.Domain;

public sealed record FichaAdministrativa(
    int Id,
    IdentidadKeycloak Identidad,
    DateTimeOffset FechaCreacionUtc,
    IdentidadKeycloak? CreadoPor,
    DateTimeOffset? FechaModificacionUtc,
    IdentidadKeycloak? ModificadoPor);
