using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class VehiculoInput
{
    public string Id { get; set; } = string.Empty;
    [Display(Name = "Propietario")]
    [Required(ErrorMessage = "Propietario: este campo es obligatorio.")]
    [StringLength(36, ErrorMessage = "Propietario: admite hasta 36 caracteres.")]
    public string ClienteId { get; set; } = string.Empty;
    [Display(Name = "Placa")]
    [Required(ErrorMessage = "Placa: este campo es obligatorio.")]
    [StringLength(10, ErrorMessage = "Placa: admite hasta 10 caracteres.")]
    public string Placa { get; set; } = string.Empty;
    [Display(Name = "Marca")]
    [Required(ErrorMessage = "Marca: este campo es obligatorio.")]
    [StringLength(60, ErrorMessage = "Marca: admite hasta 60 caracteres.")]
    public string Marca { get; set; } = string.Empty;
    [Display(Name = "Modelo")]
    [Required(ErrorMessage = "Modelo: este campo es obligatorio.")]
    [StringLength(60, ErrorMessage = "Modelo: admite hasta 60 caracteres.")]
    public string Modelo { get; set; } = string.Empty;
    [Display(Name = "Año")]
    [Range(1900, 9999, ErrorMessage = "El año no es válido.")]
    public int Anio { get; set; }
    [Display(Name = "Color")]
    [StringLength(40, ErrorMessage = "El color admite hasta 40 caracteres.")]
    public string? Color { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
