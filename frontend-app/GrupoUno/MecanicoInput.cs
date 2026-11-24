using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class MecanicoInput
{
    private const string SoloLetras = @"^\s*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+(\s+[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]+)*\s*$";

    public string Id { get; set; } = string.Empty;
    [Display(Name = "Nombres")]
    [Required(ErrorMessage = "Nombres: este campo es obligatorio.")]
    [StringLength(80, ErrorMessage = "Nombres: admite hasta 80 caracteres.")]
    [RegularExpression(SoloLetras, ErrorMessage = "Nombres: solo se admiten letras y espacios.")]
    public string Nombres { get; set; } = string.Empty;
    [Display(Name = "Apellidos")]
    [Required(ErrorMessage = "Apellidos: este campo es obligatorio.")]
    [StringLength(80, ErrorMessage = "Apellidos: admite hasta 80 caracteres.")]
    [RegularExpression(SoloLetras, ErrorMessage = "Apellidos: solo se admiten letras y espacios.")]
    public string Apellidos { get; set; } = string.Empty;
    [Display(Name = "CI")]
    [Required(ErrorMessage = "CI: este campo es obligatorio.")]
    [StringLength(20, ErrorMessage = "CI: admite hasta 20 caracteres.")]
    [RegularExpression(@"^\s*[0-9]{5,8}(-[0-9][A-Za-z])?\s*$", ErrorMessage = "El CI debe tener entre 5 y 8 dígitos y puede llevar complemento, por ejemplo 1234567-1A.")]
    public string Documento { get; set; } = string.Empty;
    [Display(Name = "Celular")]
    [Required(ErrorMessage = "Celular: este campo es obligatorio.")]
    [RegularExpression(@"^\s*[5-7][0-9]{7}\s*$", ErrorMessage = "El celular debe tener 8 dígitos y comenzar en 5, 6 o 7.")]
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
