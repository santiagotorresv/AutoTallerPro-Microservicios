using AutoTallerPro.Usuarios.Application.Identidad;

namespace AutoTallerPro.Usuarios.Infrastructure.Keycloak;

public sealed class OpcionesKeycloakAdmin
{
    public string? BaseUrl { get; set; }
    public string? Realm { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool PermitirHttpLocal { get; set; }

    internal Uri ObtenerBase(string operacion)
    {
        if (string.IsNullOrWhiteSpace(Realm) || Realm is "." or ".."
            || Realm.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))
            || string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret)
            || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(PermitirHttpLocal && uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "keycloak"))))
        {
            throw new OperacionIdentidadException(operacion, ErrorIdentidad.Configuracion);
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
