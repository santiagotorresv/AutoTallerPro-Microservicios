namespace AutoTallerPro.Usuarios.Application.Persistencia;

public sealed record FichaUsuario(int Id, string Sujeto, EstadoAlta? Estado);

public interface IConsultaFichasUsuarios
{
    Task<IReadOnlyList<FichaUsuario>> ListarAsync(string emisor, CancellationToken cancellationToken);
    Task<FichaUsuario?> ObtenerAsync(string emisor, int id, CancellationToken cancellationToken);
}

