using System.ComponentModel.DataAnnotations;

namespace AutoTallerPro.Usuarios.Application.Validaciones;

public static class ReglasCuenta
{
    internal const int LongitudMinimaUsername = 3;
    internal const int LongitudMaximaUsername = 50;

    public static bool UsuarioValido(string? username) =>
        username is { Length: >= LongitudMinimaUsername and <= LongitudMaximaUsername } &&
        username.All(EsCaracterDeUsername);

    public static bool EmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && email == email.Trim() &&
        new EmailAddressAttribute().IsValid(email);

    internal static bool EsCaracterDeUsername(char caracter) =>
        char.IsAsciiLetterOrDigit(caracter) || caracter is '.' or '_' or '-';
}
