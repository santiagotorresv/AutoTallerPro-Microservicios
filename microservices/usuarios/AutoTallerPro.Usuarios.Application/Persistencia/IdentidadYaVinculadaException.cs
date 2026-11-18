namespace AutoTallerPro.Usuarios.Application.Persistencia;

public sealed class IdentidadYaVinculadaException(Exception innerException)
    : InvalidOperationException("La identidad de Keycloak ya tiene una ficha administrativa.", innerException);
