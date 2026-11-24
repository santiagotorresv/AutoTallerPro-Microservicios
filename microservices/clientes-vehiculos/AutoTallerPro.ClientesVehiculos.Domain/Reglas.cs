using System.Net.Mail;
using System.Text.RegularExpressions;
namespace AutoTallerPro.ClientesVehiculos.Domain;

public static class Reglas
{
    public static bool Texto(string? valor, int maximo) => !string.IsNullOrWhiteSpace(valor) && valor.Trim().Length <= maximo;
    public static bool Opcional(string? valor, int maximo) => valor is null || valor.Trim().Length <= maximo;
    public static bool Correo(string? valor) => string.IsNullOrWhiteSpace(valor)
        || (valor.Trim().Length <= 254 && MailAddress.TryCreate(valor.Trim(), out var correo) && correo.Address == valor.Trim());
    public static string SinEspacios(string? valor) => Regex.Replace(valor ?? "", @"\s+", "").ToUpperInvariant();
    public static bool CiNit(string valor) => Regex.IsMatch(valor, @"^[0-9]{5,13}(-[0-9][A-Z])?$");
    public static bool Celular(string valor) => Regex.IsMatch(valor, "^[67][0-9]{7}$");
}
