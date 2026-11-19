using Npgsql;
namespace AutoTallerPro.ClientesVehiculos.Infrastructure.Persistencia;
public sealed class Conexion(string cadena)
{
    public NpgsqlConnection Crear() => new(cadena);
}
