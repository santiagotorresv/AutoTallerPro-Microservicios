using System.ComponentModel.DataAnnotations;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class UsuarioCreateInput
{
    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "Ingrese el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    [Required(ErrorMessage = "Seleccione un rol.")]
    [RegularExpression("^(Administrador|Recepcionista)$", ErrorMessage = "Seleccione un rol válido.")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Ingrese el nombre.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Display(Name = "Apellido")]
    [Required(ErrorMessage = "Ingrese el apellido.")]
    [StringLength(100, ErrorMessage = "El apellido admite hasta 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;
}
