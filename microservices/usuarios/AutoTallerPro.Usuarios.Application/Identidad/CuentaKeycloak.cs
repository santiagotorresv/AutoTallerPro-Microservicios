namespace AutoTallerPro.Usuarios.Application.Identidad;

public sealed record CuentaKeycloak(
    string Id,
    string Username,
    string? Correo,
    string? Nombre,
    string? Apellido,
    bool Habilitada,
    bool CorreoVerificado,
    IReadOnlyList<string> AccionesRequeridas,
    Guid? CorrelacionAlta = null);

public sealed record NuevaCuentaKeycloak(
    string Username, string Correo, string? Nombre = null, string? Apellido = null, Guid? CorrelacionAlta = null);

public sealed record DatosCuentaKeycloak(string? NuevoCorreo = null, string? Nombre = null, string? Apellido = null);
