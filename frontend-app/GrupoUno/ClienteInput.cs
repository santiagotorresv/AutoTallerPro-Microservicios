using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class ClienteInput
{
    public string Id { get; set; } = string.Empty;
    // Bitácora: solo se muestra; la API la toma del token, no del formulario.
    public string? UsuarioRegistro { get; set; }
    public string? UsuarioActualizacion { get; set; }
    [Display(Name = "CI/NIT")]
    [Required(ErrorMessage = "CI/NIT: este campo es obligatorio.")]
    [StringLength(20, ErrorMessage = "CI/NIT: admite hasta 20 caracteres.")]
    [RegularExpression(@"^\s*[0-9]{5,13}(-[0-9][A-Za-z])?\s*$", ErrorMessage = "El CI/NIT debe tener entre 5 y 13 dígitos; el CI puede llevar complemento, por ejemplo 1234567-1A.")]
    public string Documento { get; set; } = string.Empty;
    [Display(Name = "Nombre o razón social")]
    [Required(ErrorMessage = "Nombre o razón social: este campo es obligatorio.")]
    [StringLength(150, ErrorMessage = "Nombre o razón social: admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Celular")]
    [Required(ErrorMessage = "Celular: este campo es obligatorio.")]
    [RegularExpression(@"^\s*[67][0-9]{7}\s*$", ErrorMessage = "El celular debe tener 8 dígitos y comenzar en 6 o 7.")]
    public string? Telefono { get; set; }
    [Display(Name = "Correo electrónico")]
    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    public string? Correo { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
