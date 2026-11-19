using MySqlConnector;
namespace AutoTallerPro.Servicios.Infrastructure.Persistencia;
public sealed class Conexion(string cadena)
{
    public MySqlConnection Crear() => new(cadena);
}
