namespace AutoTallerPro.Usuarios.Domain;

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";

    public static bool EsDeNegocio(string? rol) => rol is Administrador or Recepcionista;
}
