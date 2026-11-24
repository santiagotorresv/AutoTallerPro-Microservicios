using System.Text.RegularExpressions;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.ClientesVehiculos.Domain;
public sealed class Vehiculo
{
    public const string PatronPlaca = "^[0-9]{3,4}[A-Z]{3}$";
    public const string PatronPlacaExtranjera = "^[A-Z0-9]{2,15}$";

    public Guid Id { get; }
    public Guid ClienteId { get; }
    public string Placa { get; }
    public bool EsPlacaExtranjera { get; }
    public string Marca { get; }
    public string Modelo { get; }
    public int Anio { get; }
    public string? Color { get; }
    public bool Activo { get; }
    public DateTimeOffset FechaRegistro { get; }
    public DateTimeOffset FechaActualizacion { get; }
    public string? UsuarioRegistro { get; }
    public string? UsuarioActualizacion { get; }
    private Vehiculo(Guid id, Guid clienteId, string placa, bool esPlacaExtranjera, string marca, string modelo, int anio, string? color, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion, string? usuarioRegistro, string? usuarioActualizacion) { Id = id; ClienteId = clienteId; Placa = placa; EsPlacaExtranjera = esPlacaExtranjera; Marca = marca; Modelo = modelo; Anio = anio; Color = color; Activo = activo; FechaRegistro = fechaRegistro; FechaActualizacion = fechaActualizacion; UsuarioRegistro = usuarioRegistro; UsuarioActualizacion = usuarioActualizacion; }
    public static Resultado<Vehiculo> Crear(Guid id, Guid clienteId, string placa, string marca, string modelo, int anio, string? color, bool activo, DateTimeOffset? fechaRegistro = null, DateTimeOffset? fechaActualizacion = null, bool esPlacaExtranjera = false, string? usuarioRegistro = null, string? usuarioActualizacion = null)
    {
        if (id == Guid.Empty || clienteId == Guid.Empty) return Resultado<Vehiculo>.Fallo("vehiculo.id", "Los identificadores del vehículo y del propietario son obligatorios.");
        var normalizada = NormalizarPlaca(placa);
        if (normalizada.Length == 0) return Resultado<Vehiculo>.Fallo("vehiculo.placa", "La placa es obligatoria.");
        if (!Regex.IsMatch(normalizada, esPlacaExtranjera ? PatronPlacaExtranjera : PatronPlaca))
            return Resultado<Vehiculo>.Fallo("vehiculo.placa", esPlacaExtranjera
                ? "La placa extranjera debe contener entre 2 y 15 letras o números, en cualquier orden."
                : "La placa debe contener 3 o 4 números y 3 letras. Ejemplos: 123ABC o 1234ABC.");
        if (!Reglas.Texto(marca, 60) || !Reglas.Texto(modelo, 60)) return Resultado<Vehiculo>.Fallo("vehiculo.datos", "La marca y el modelo son obligatorios y admiten hasta 60 caracteres.");
        if (anio < 1900 || anio > DateTime.UtcNow.Year + 1) return Resultado<Vehiculo>.Fallo("vehiculo.anio", "El año debe estar entre 1900 y el próximo año.");
        if (!Reglas.Opcional(color, 40)) return Resultado<Vehiculo>.Fallo("vehiculo.color", "El color admite hasta 40 caracteres.");
        return Resultado<Vehiculo>.Exito(new(id, clienteId, normalizada, esPlacaExtranjera, marca.Trim(), modelo.Trim(), anio, string.IsNullOrWhiteSpace(color) ? null : color.Trim(), activo, fechaRegistro ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), fechaActualizacion ?? DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), usuarioRegistro, usuarioActualizacion));
    }

    // Reconstruye un registro ya guardado; las reglas se aplican al crear o editar.
    public static Vehiculo Restaurar(Guid id, Guid clienteId, string placa, bool esPlacaExtranjera, string marca, string modelo, int anio, string? color, bool activo, DateTimeOffset fechaRegistro, DateTimeOffset fechaActualizacion, string? usuarioRegistro = null, string? usuarioActualizacion = null)
        => new(id, clienteId, placa, esPlacaExtranjera, marca, modelo, anio, color, activo, fechaRegistro, fechaActualizacion, usuarioRegistro, usuarioActualizacion);

    private static string NormalizarPlaca(string? placa) => Regex.Replace(placa ?? "", @"[\s-]", "").ToUpperInvariant();
}
