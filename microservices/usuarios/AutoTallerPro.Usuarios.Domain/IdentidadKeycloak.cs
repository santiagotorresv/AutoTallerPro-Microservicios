namespace AutoTallerPro.Usuarios.Domain;

public sealed record IdentidadKeycloak
{
    public IdentidadKeycloak(string emisor, string sujeto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emisor);
        ArgumentException.ThrowIfNullOrWhiteSpace(sujeto);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(emisor.Length, 255, nameof(emisor));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sujeto.Length, 255, nameof(sujeto));

        Emisor = emisor;
        Sujeto = sujeto;
    }

    public string Emisor { get; }
    public string Sujeto { get; }
}
