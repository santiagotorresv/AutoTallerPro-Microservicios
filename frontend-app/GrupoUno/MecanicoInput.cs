using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class MecanicoInput
{
    public string Id { get; set; } = string.Empty;
    [Display(Name = "Nombres")]
    [Required(ErrorMessage = "Nombres: este campo es obligatorio.")]
    [StringLength(80, ErrorMessage = "Nombres: admite hasta 80 caracteres.")]
    public string Nombres { get; set; } = string.Empty;
    [Display(Name = "Apellidos")]
    [Required(ErrorMessage = "Apellidos: este campo es obligatorio.")]
    [StringLength(80, ErrorMessage = "Apellidos: admite hasta 80 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;
    [Display(Name = "Documento de identidad")]
    [Required(ErrorMessage = "Documento de identidad: este campo es obligatorio.")]
    [StringLength(20, ErrorMessage = "Documento de identidad: admite hasta 20 caracteres.")]
    public string Documento { get; set; } = string.Empty;
    [Display(Name = "Teléfono")]
    [RegularExpression(@"^\+?[0-9]{7,15}$", ErrorMessage = "El teléfono debe tener entre 7 y 15 dígitos y puede iniciar con +.")]
    public string? Telefono { get; set; }
    [Display(Name = "Correo electrónico")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string? Correo { get; set; }
    [Display(Name = "Especialidad")]
    [Required(ErrorMessage = "Especialidad: este campo es obligatorio.")]
    [StringLength(60, ErrorMessage = "Especialidad: admite hasta 60 caracteres.")]
    public string Especialidad { get; set; } = string.Empty;
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
