namespace AutoTallerPro.Usuarios.Domain;

public enum ResultadoAuditoria
{
    Exitoso,
    Fallido,
    Pendiente
}

public sealed record RegistroAuditoria(
    IdentidadKeycloak Actor,
    string Accion,
    string Entidad,
    string ObjetivoId,
    DateTimeOffset FechaUtc,
    ResultadoAuditoria Resultado,
    string? CodigoMotivo = null,
    string? CorrelacionId = null)
{
    public long Id { get; init; }
}
