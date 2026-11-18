using System.Globalization;
using System.Text;
using AutoTallerPro.Usuarios.Application.Validaciones;

namespace AutoTallerPro.Usuarios.Application.Servicios;

public static class GeneradorNombreUsuarioService
{
    private const int MaximoSufijo = 100;

    public static IEnumerable<string> GenerarCandidatos(string correoElectronico)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correoElectronico);

        string baseUsuario = Normalizar(correoElectronico);
        if (baseUsuario.Length < ReglasCuenta.LongitudMinimaUsername)
        {
            baseUsuario = "usuario";
        }

        baseUsuario = Recortar(baseUsuario, ReglasCuenta.LongitudMaximaUsername);
        yield return baseUsuario;

        for (int sufijo = 1; sufijo <= MaximoSufijo; sufijo++)
        {
            string sufijoTexto = "-" + sufijo.ToString(CultureInfo.InvariantCulture);
            yield return Recortar(baseUsuario, ReglasCuenta.LongitudMaximaUsername - sufijoTexto.Length)
                + sufijoTexto;
        }
    }

    public static async Task<string> GenerarUsuarioUnicoAsync(
        string correoElectronico,
        Func<string, CancellationToken, Task<bool>> existeUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(existeUsername);

        foreach (string candidato in GenerarCandidatos(correoElectronico))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await existeUsername(candidato, cancellationToken))
            {
                return candidato;
            }
        }

        throw new InvalidOperationException("No se pudo generar un nombre de usuario único.");
    }

    private static string Normalizar(string correoElectronico)
    {
        string parteLocal = correoElectronico.Split('@', 2)[0].Trim();
        string texto = parteLocal.Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var usuario = new StringBuilder();

        foreach (char caracter in texto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ReglasCuenta.EsCaracterDeUsername(caracter))
            {
                usuario.Append(caracter);
            }
            else if (usuario.Length > 0 && usuario[^1] != '-')
            {
                usuario.Append('-');
            }
        }

        return usuario.ToString().Trim('-');
    }

    private static string Recortar(string valor, int longitud) =>
        valor[..Math.Min(valor.Length, longitud)];
}
