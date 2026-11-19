using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class ClienteInput
{
    public string Id { get; set; } = string.Empty;
    [Display(Name = "CI/NIT")]
    [Required(ErrorMessage = "CI/NIT: este campo es obligatorio.")]
    [StringLength(20, ErrorMessage = "CI/NIT: admite hasta 20 caracteres.")]
    public string Documento { get; set; } = string.Empty;
    [Display(Name = "Nombre o razón social")]
    [Required(ErrorMessage = "Nombre o razón social: este campo es obligatorio.")]
    [StringLength(150, ErrorMessage = "Nombre o razón social: admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Teléfono")]
    [RegularExpression(@"^\+?[0-9]{7,15}$", ErrorMessage = "El teléfono debe tener entre 7 y 15 dígitos y puede iniciar con +.")]
    public string? Telefono { get; set; }
    [Display(Name = "Correo electrónico")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string? Correo { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
