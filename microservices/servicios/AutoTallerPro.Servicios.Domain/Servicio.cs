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
        var nombreLimpio = Reglas.UnEspacio(nombre);
        if (!Reglas.Texto(nombreLimpio, 120)) return Resultado<Servicio>.Fallo("servicio.nombre", "El nombre del servicio es obligatorio y admite hasta 120 caracteres.");
        if (!Reglas.NombreServicio(nombreLimpio)) return Resultado<Servicio>.Fallo("servicio.nombre", "El nombre del servicio solo admite letras, números, espacios y los signos ( ) . / -.");
        if (!Reglas.Opcional(descripcion, 500)) return Resultado<Servicio>.Fallo("servicio.descripcion", "La descripción admite hasta 500 caracteres.");
        if (precioBase <= 0 || precioBase > 9999999999.99m || decimal.Round(precioBase, 2) != precioBase) return Resultado<Servicio>.Fallo("servicio.precio", "El precio debe ser mayor a cero, no superar 9999999999,99 y tener hasta dos decimales.");
        if (duracionEstimada is <= 0 or > 10080) return Resultado<Servicio>.Fallo("servicio.duracion", "La duración estimada debe estar entre 1 y 10080 minutos cuando se indique.");
        return Resultado<Servicio>.Exito(new(id, categoriaServicioId, nombreLimpio, string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(), precioBase, duracionEstimada, activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }

    // Reconstruye un registro ya guardado; las reglas se aplican al crear o editar.
    public static Servicio Restaurar(Guid id, Guid categoriaServicioId, string nombre, string? descripcion, decimal precioBase, int? duracionEstimada, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion)
        => new(id, categoriaServicioId, nombre, descripcion, precioBase, duracionEstimada, activo, fechaRegistro, fechaActualizacion);
}
