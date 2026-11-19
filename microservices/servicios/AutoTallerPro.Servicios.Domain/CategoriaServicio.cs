using AutoTallerPro.Servicios.Domain.Resultados;
namespace AutoTallerPro.Servicios.Domain;
public sealed class CategoriaServicio
{
    public Guid Id { get; }
    public string Nombre { get; }
    public string? Descripcion { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    private CategoriaServicio(Guid id, string nombre, string? descripcion, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion) { Id = id; Nombre = nombre; Descripcion = descripcion; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; }
    public static Resultado<CategoriaServicio> Crear(Guid id, string nombre, string? descripcion, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null)
    {
        if (id == Guid.Empty) return Resultado<CategoriaServicio>.Fallo("categoria.id", "El identificador de la categoría no es válido.");
        if (!Reglas.Texto(nombre, 100)) return Resultado<CategoriaServicio>.Fallo("categoria.nombre", "El nombre de la categoría es obligatorio y admite hasta 100 caracteres.");
        if (!Reglas.Opcional(descripcion, 500)) return Resultado<CategoriaServicio>.Fallo("categoria.descripcion", "La descripción admite hasta 500 caracteres.");
        return Resultado<CategoriaServicio>.Exito(new(id, nombre.Trim(), string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }
}
