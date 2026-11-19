using System.ComponentModel.DataAnnotations;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class UsuarioEditInput
{
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "Ingrese el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    [Required(ErrorMessage = "Seleccione un rol.")]
    [RegularExpression("^(Administrador|Recepcionista)$", ErrorMessage = "Seleccione un rol válido.")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Activo")]
    public bool Activo { get; set; }
}
