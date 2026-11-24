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
        var nombresLimpios = Reglas.UnEspacio(nombres);
        var apellidosLimpios = Reglas.UnEspacio(apellidos);
        if (nombresLimpios.Length == 0 || apellidosLimpios.Length == 0) return Resultado<Mecanico>.Fallo("mecanico.nombre", "Los nombres y apellidos son obligatorios.");
        if (!Reglas.SoloLetras(nombresLimpios, 80) || !Reglas.SoloLetras(apellidosLimpios, 80)) return Resultado<Mecanico>.Fallo("mecanico.nombre", "Los nombres y apellidos solo admiten letras y espacios, hasta 80 caracteres cada uno.");
        var ci = Reglas.SinEspacios(documento);
        if (!Reglas.Ci(ci)) return Resultado<Mecanico>.Fallo("mecanico.documento", "El CI debe tener entre 5 y 8 dígitos y puede llevar complemento, por ejemplo 1234567-1A.");
        var celular = Reglas.SinEspacios(telefono);
        if (celular.Length == 0) return Resultado<Mecanico>.Fallo("mecanico.telefono", "El celular es obligatorio.");
        if (!Reglas.Celular(celular)) return Resultado<Mecanico>.Fallo("mecanico.telefono", "El celular debe tener 8 dígitos y comenzar en 5, 6 o 7.");
        if (!Reglas.Correo(correo)) return Resultado<Mecanico>.Fallo("mecanico.correo", "El correo electrónico no es válido.");
        if (!Especialidades.Contains(especialidad?.Trim() ?? "", StringComparer.Ordinal)) return Resultado<Mecanico>.Fallo("mecanico.especialidad", "Seleccione una especialidad válida: Mecánica general, Electricidad, Diagnóstico o Carrocería.");
        return Resultado<Mecanico>.Exito(new(id.Trim().ToLowerInvariant(), nombresLimpios, apellidosLimpios, ci, celular, string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(), especialidad!.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }

    // Reconstruye un registro ya guardado; las reglas se aplican al crear o editar.
    public static Mecanico Restaurar(string id, string nombres, string apellidos, string documento, string? telefono, string? correo, string especialidad, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion)
        => new(id, nombres, apellidos, documento, telefono, correo, especialidad, activo, fechaRegistro, fechaActualizacion);
}
