using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class ServicioInput
{
    public string Id { get; set; } = string.Empty;
    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "Categoría: este campo es obligatorio.")]
    [StringLength(36, ErrorMessage = "Categoría: admite hasta 36 caracteres.")]
    public string CategoriaServicioId { get; set; } = string.Empty;
    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Nombre: este campo es obligatorio.")]
    [StringLength(120, ErrorMessage = "Nombre: admite hasta 120 caracteres.")]
    [RegularExpression(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9 ()./-]+$", ErrorMessage = "El nombre solo admite letras, números, espacios y los signos ( ) . / -.")]
    public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Descripción")]
    [StringLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres.")]
    public string? Descripcion { get; set; }
    [Display(Name = "Precio base (Bs)")]
    [Required(ErrorMessage = "Precio base: este campo es obligatorio.")]
    [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "El precio debe ser mayor a cero y no superar 9999999999,99.")]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(DecimalMonedaBinder))]
    public decimal PrecioBase { get; set; }
    [Display(Name = "Duración estimada (minutos)")]
    [Range(1, 10080, ErrorMessage = "La duración debe estar entre 1 y 10080 minutos.")]
    public int? DuracionEstimada { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
