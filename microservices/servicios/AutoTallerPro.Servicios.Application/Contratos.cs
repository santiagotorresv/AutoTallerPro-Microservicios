namespace AutoTallerPro.Servicios.Application;
public sealed record DatosCategoriaServicio(string Nombre, string? Descripcion, bool Activo = true);
public sealed record DatosServicio(Guid CategoriaServicioId, string Nombre, string? Descripcion, decimal PrecioBase, int? DuracionEstimada, bool Activo = true);
