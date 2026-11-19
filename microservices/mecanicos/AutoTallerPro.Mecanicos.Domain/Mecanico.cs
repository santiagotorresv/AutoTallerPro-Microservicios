using AutoTallerPro.Mecanicos.Domain.Resultados;
namespace AutoTallerPro.Mecanicos.Domain;
public sealed class Mecanico
{
    public static IReadOnlyList<string> Especialidades { get; } = Array.AsReadOnly(new[] { "Mecánica general", "Electricidad", "Diagnóstico", "Carrocería" });
    public static bool IdValido(string? id) => id is not null && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]{24}$");

    public string Id { get; }
    public string Nombres { get; }
    public string Apellidos { get; }
    public string Documento { get; }
    public string? Telefono { get; }
    public string? Correo { get; }
    public string Especialidad { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    private Mecanico(string id, string nombres, string apellidos, string documento, string? telefono, string? correo, string especialidad, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion) { Id = id; Nombres = nombres; Apellidos = apellidos; Documento = documento; Telefono = telefono; Correo = correo; Especialidad = especialidad; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; }
    public static Resultado<Mecanico> Crear(string id, string nombres, string apellidos, string documento, string? telefono, string? correo, string especialidad, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null)
    {
        if (!IdValido(id)) return Resultado<Mecanico>.Fallo("mecanico.id", "El identificador del mecánico debe tener 24 caracteres hexadecimales.");
        if (!Reglas.Texto(nombres, 80) || !Reglas.Texto(apellidos, 80)) return Resultado<Mecanico>.Fallo("mecanico.nombre", "Los nombres y apellidos son obligatorios y admiten hasta 80 caracteres cada uno.");
        if (!Reglas.Documento(documento)) return Resultado<Mecanico>.Fallo("mecanico.documento", "El documento debe tener entre 4 y 20 letras, números o guiones.");
        if (!Reglas.Telefono(telefono)) return Resultado<Mecanico>.Fallo("mecanico.telefono", "El teléfono debe contener entre 7 y 15 dígitos y puede iniciar con +.");
        if (!Reglas.Correo(correo)) return Resultado<Mecanico>.Fallo("mecanico.correo", "El correo electrónico no es válido.");
        if (!Especialidades.Contains(especialidad?.Trim() ?? "", StringComparer.Ordinal)) return Resultado<Mecanico>.Fallo("mecanico.especialidad", "Seleccione una especialidad válida: Mecánica general, Electricidad, Diagnóstico o Carrocería.");
        return Resultado<Mecanico>.Exito(new(id.Trim().ToLowerInvariant(), nombres.Trim(), apellidos.Trim(), (documento.Trim()).ToUpperInvariant(), string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim(), string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(), especialidad!.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }
}
