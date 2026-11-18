using System.ComponentModel.DataAnnotations;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class UsuarioCreateInput
{
    [Display(Name = "Correo electrónico")]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Confirmar contraseña")]
    [DataType(DataType.Password)]
    public string ConfirmarPassword { get; set; } = string.Empty;
}
