using System.ComponentModel.DataAnnotations;
namespace AutoTallerPro.Frontend.GrupoUno;
public sealed class CategoriaServicioInput
{
    public string Id { get; set; } = string.Empty;
    // Bitácora: solo se muestra; la API la toma del token, no del formulario.
    public string? UsuarioRegistro { get; set; }
    public string? UsuarioActualizacion { get; set; }
    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Nombre: este campo es obligatorio.")]
    [StringLength(100, ErrorMessage = "Nombre: admite hasta 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [Display(Name = "Descripción")]
    [StringLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres.")]
    public string? Descripcion { get; set; }
    [Display(Name = "Activo")]

    public bool Activo { get; set; } = true;
}
