using AutoTallerPro.Servicios.Domain.Resultados;
namespace AutoTallerPro.Servicios.Domain;
public sealed class Servicio
{
    public Guid Id { get; }
    public Guid CategoriaServicioId { get; }
    public string Nombre { get; }
    public string? Descripcion { get; }
    public decimal PrecioBase { get; }
    public int? DuracionEstimada { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    private Servicio(Guid id, Guid categoriaServicioId, string nombre, string? descripcion, decimal precioBase, int? duracionEstimada, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion) { Id = id; CategoriaServicioId = categoriaServicioId; Nombre = nombre; Descripcion = descripcion; PrecioBase = precioBase; DuracionEstimada = duracionEstimada; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; }
    public static Resultado<Servicio> Crear(Guid id, Guid categoriaServicioId, string nombre, string? descripcion, decimal precioBase, int? duracionEstimada, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null)
    {
        if (id == Guid.Empty || categoriaServicioId == Guid.Empty) return Resultado<Servicio>.Fallo("servicio.id", "Los identificadores del servicio y de la categoría son obligatorios.");
        if (!Reglas.Texto(nombre, 120)) return Resultado<Servicio>.Fallo("servicio.nombre", "El nombre del servicio es obligatorio y admite hasta 120 caracteres.");
        if (!Reglas.Opcional(descripcion, 500)) return Resultado<Servicio>.Fallo("servicio.descripcion", "La descripción admite hasta 500 caracteres.");
        if (precioBase < 0 || precioBase > 9999999999.99m || decimal.Round(precioBase, 2) != precioBase) return Resultado<Servicio>.Fallo("servicio.precio", "El precio debe estar entre 0 y 9999999999,99 y admitir hasta dos decimales.");
        if (duracionEstimada is <= 0 or > 10080) return Resultado<Servicio>.Fallo("servicio.duracion", "La duración estimada debe estar entre 1 y 10080 minutos cuando se indique.");
        return Resultado<Servicio>.Exito(new(id, categoriaServicioId, nombre.Trim(), string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(), precioBase, duracionEstimada, activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }
}
