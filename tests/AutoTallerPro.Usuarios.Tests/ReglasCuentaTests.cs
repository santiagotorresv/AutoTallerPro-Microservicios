using AutoTallerPro.Usuarios.Application.Validaciones;
using AutoTallerPro.Usuarios.Domain;

namespace AutoTallerPro.Usuarios.Tests;

public sealed class ReglasCuentaTests
{
    [Theory]
    [InlineData("empleado123", true)]
    [InlineData("Juan.Perez", true)]
    [InlineData("maria_lopez", true)]
    [InlineData("usuario-01", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("usuario con espacios", false)]
    [InlineData("usuario@correo", false)]
    [InlineData("María", false)]
    [InlineData("Ana東京", false)]
    [InlineData("ana١", false)]
    public void UsuarioValido_AplicaLaLongitudYElAlfabetoCompartido(string? username, bool esperado)
    {
        Assert.Equal(esperado, ReglasCuenta.UsuarioValido(username));
    }

    [Fact]
    public void UsuarioValido_Acepta50CaracteresYRechaza51()
    {
        Assert.True(ReglasCuenta.UsuarioValido(new string('a', 50)));
        Assert.False(ReglasCuenta.UsuarioValido(new string('a', 51)));
    }

    [Theory]
    [InlineData("persona@gmail.com", true)]
    [InlineData("persona@outlook.com", true)]
    [InlineData("persona@hotmail.com", true)]
    [InlineData("empleado@empresa.com.bo", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("correo-invalido", false)]
    [InlineData("persona@", false)]
    [InlineData(" persona@gmail.com", false)]
    [InlineData("persona@gmail.com ", false)]
    public void EmailValido_ExigeFormatoYSinEspaciosExtremos(string? email, bool esperado)
    {
        Assert.Equal(esperado, ReglasCuenta.EmailValido(email));
    }

    [Fact]
    public void EmailValido_Acepta254CaracteresYRechaza255()
    {
        var dominio = string.Join(".", new string('a', 63), new string('b', 63), new string('c', 59), "co");
        var email = new string('a', 63) + "@" + dominio;

        Assert.Equal(254, email.Length);
        Assert.True(ReglasCuenta.EmailValido(email));
        Assert.False(ReglasCuenta.EmailValido("a" + email));
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("Recepcionista", true)]
    [InlineData("administrador", false)]
    [InlineData("Recepcionista ", false)]
    [InlineData("realm-admin", false)]
    [InlineData(null, false)]
    public void EsDeNegocio_AdmiteSoloLosRolesExactos(string? rol, bool esperado)
    {
        Assert.Equal(esperado, Roles.EsDeNegocio(rol));
    }
}
