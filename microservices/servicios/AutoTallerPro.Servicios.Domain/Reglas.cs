using System.Net.Mail;
using System.Text.RegularExpressions;
namespace AutoTallerPro.Servicios.Domain;

public static class Reglas
{
    public static bool Texto(string? valor, int maximo) => !string.IsNullOrWhiteSpace(valor) && valor.Trim().Length <= maximo;
    public static bool Opcional(string? valor, int maximo) => valor is null || valor.Trim().Length <= maximo;
    public static bool Correo(string? valor) => string.IsNullOrWhiteSpace(valor)
        || (valor.Trim().Length <= 254 && MailAddress.TryCreate(valor.Trim(), out var correo) && correo.Address == valor.Trim());
    public static bool Telefono(string? valor) => string.IsNullOrWhiteSpace(valor)
        || Regex.IsMatch(valor.Trim(), @"^\+?[0-9]{7,15}$");
    public static bool Documento(string? valor) => valor is not null && Regex.IsMatch(valor.Trim(), @"^[A-Za-z0-9][A-Za-z0-9\-]{3,19}$");
}
