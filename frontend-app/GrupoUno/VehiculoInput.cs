using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class VehiculoInput : IValidatableObject
{
    public string Id { get; set; } = string.Empty;
    // Bitácora: solo se muestra; la API la toma del token, no del formulario.
    public string? UsuarioRegistro { get; set; }
    public string? UsuarioActualizacion { get; set; }
    [Display(Name = "Propietario")]
    [Required(ErrorMessage = "Propietario: este campo es obligatorio.")]
    [StringLength(36, ErrorMessage = "Propietario: admite hasta 36 caracteres.")]
    public string ClienteId { get; set; } = string.Empty;
    [Display(Name = "Placa")]
    [Required(ErrorMessage = "Placa: este campo es obligatorio.")]
    [StringLength(20, ErrorMessage = "Placa: admite hasta 15 letras o números.")]
    public string Placa { get; set; } = string.Empty;
    [Display(Name = "Placa extranjera")]
    public bool EsPlacaExtranjera { get; set; }
    [Display(Name = "Marca")]
    [Required(ErrorMessage = "Marca: este campo es obligatorio.")]
    [StringLength(60, ErrorMessage = "Marca: admite hasta 60 caracteres.")]
    public string Marca { get; set; } = string.Empty;
    [Display(Name = "Modelo")]
    [Required(ErrorMessage = "Modelo: este campo es obligatorio.")]
    [StringLength(60, ErrorMessage = "Modelo: admite hasta 60 caracteres.")]
    public string Modelo { get; set; } = string.Empty;
    [Display(Name = "Año")]
    [Required(ErrorMessage = "Año: este campo es obligatorio.")]
    [Range(1900, 9999, ErrorMessage = "El año no es válido.")]
    public int Anio { get; set; }
    [Display(Name = "Color")]
    [StringLength(40, ErrorMessage = "El color admite hasta 40 caracteres.")]
    public string? Color { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        var placa = Regex.Replace(Placa ?? "", @"[\s-]", "").ToUpperInvariant();
        if (placa.Length == 0) yield break;
        if (EsPlacaExtranjera && !Regex.IsMatch(placa, "^[A-Z0-9]{2,15}$"))
            yield return new("La placa extranjera debe contener entre 2 y 15 letras o números, en cualquier orden.", [nameof(Placa)]);
        else if (!EsPlacaExtranjera && !Regex.IsMatch(placa, "^[0-9]{3,4}[A-Z]{3}$"))
            yield return new("La placa debe contener 3 o 4 números y 3 letras. Ejemplos: 123ABC o 1234ABC.", [nameof(Placa)]);
    }
}
