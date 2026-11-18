using System.ComponentModel.DataAnnotations;

namespace AutoTallerPro.Frontend.Pages.Administracion.Usuarios;

public sealed class UsuarioEditInput
{
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    public string Rol { get; set; } = string.Empty;

    [Display(Name = "Activo")]
    public bool? Activo { get; set; }
}
