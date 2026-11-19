namespace AutoTallerPro.Mecanicos.Application;
public sealed record DatosMecanico(string Nombres, string Apellidos, string Documento, string? Telefono, string? Correo, string Especialidad, bool Activo = true);
