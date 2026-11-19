namespace AutoTallerPro.ClientesVehiculos.Application;
public sealed record DatosCliente(string Documento, string Nombre, string? Telefono, string? Correo, bool Activo = true);
public sealed record DatosVehiculo(Guid ClienteId, string Placa, string Marca, string Modelo, int Anio, string? Color, bool Activo = true);
