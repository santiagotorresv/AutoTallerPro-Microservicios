using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.ClientesVehiculos.Domain;
public sealed class Vehiculo
{
    public Guid Id { get; }
    public Guid ClienteId { get; }
    public string Placa { get; }
    public string Marca { get; }
    public string Modelo { get; }
    public int Anio { get; }
    public string? Color { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    private Vehiculo(Guid id, Guid clienteId, string placa, string marca, string modelo, int anio, string? color, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion) { Id = id; ClienteId = clienteId; Placa = placa; Marca = marca; Modelo = modelo; Anio = anio; Color = color; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; }
    public static Resultado<Vehiculo> Crear(Guid id, Guid clienteId, string placa, string marca, string modelo, int anio, string? color, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null)
    {
        if (id == Guid.Empty || clienteId == Guid.Empty) return Resultado<Vehiculo>.Fallo("vehiculo.id", "Los identificadores del vehículo y del propietario son obligatorios.");
        if (placa is null || !System.Text.RegularExpressions.Regex.IsMatch(placa.Trim(), @"^[A-Za-z0-9]{3,5}-?[A-Za-z0-9]{2,4}$")) return Resultado<Vehiculo>.Fallo("vehiculo.placa", "La placa debe tener de 5 a 9 letras o números, con un guion opcional.");
        if (!Reglas.Texto(marca, 60) || !Reglas.Texto(modelo, 60)) return Resultado<Vehiculo>.Fallo("vehiculo.datos", "La marca y el modelo son obligatorios y admiten hasta 60 caracteres.");
        if (anio < 1900 || anio > DateTime.UtcNow.Year + 1) return Resultado<Vehiculo>.Fallo("vehiculo.anio", "El año debe estar entre 1900 y el próximo año.");
        if (!Reglas.Opcional(color, 40)) return Resultado<Vehiculo>.Fallo("vehiculo.color", "El color admite hasta 40 caracteres.");
        return Resultado<Vehiculo>.Exito(new(id, clienteId, placa.Trim().Replace("-", "").ToUpperInvariant(), marca.Trim(), modelo.Trim(), anio, string.IsNullOrWhiteSpace(color) ? null : color.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
    }
}
