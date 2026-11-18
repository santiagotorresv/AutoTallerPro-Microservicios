using AutoTallerPro.Usuarios.Domain.Resultados;
using AutoTallerPro.Usuarios.Domain.Validaciones;

namespace AutoTallerPro.Usuarios.Tests;

public sealed class ResultadoValidacionTests
{
    [Fact]
    public void Validacion_DevuelveExitoOFalloConUnCodigoDeNegocio()
    {
        var valido = Validacion.Requerir(true, ErrorUsuarios.DatosInvalidos);
        Assert.True(valido.EsExitoso);
        Assert.Null(valido.Error);

        var invalido = Validacion.Requerir(false, ErrorUsuarios.DatosInvalidos);
        Assert.False(invalido.EsExitoso);
        Assert.Equal(ErrorUsuarios.DatosInvalidos, invalido.Error!.Codigo);
    }

    [Fact]
    public void Validacion_ConstruyeElValorSoloCuandoLaCondicionSeCumple()
    {
        int construcciones = 0;
        int CrearValor() => ++construcciones;

        var invalido = Validacion.Requerir(false, ErrorUsuarios.DatosInvalidos, CrearValor);
        Assert.False(invalido.EsExitoso);
        Assert.Equal(0, construcciones);
        Assert.Throws<InvalidOperationException>(() => invalido.Valor);

        var valido = Validacion.Requerir(true, ErrorUsuarios.DatosInvalidos, CrearValor);
        Assert.True(valido.EsExitoso);
        Assert.Equal(1, valido.Valor);
        Assert.Equal(1, construcciones);
    }

    [Fact]
    public void Resultado_DistingueAusenciaValidaDeUnFalloConEspera()
    {
        var ausencia = Resultado<string?>.Exitoso(null);
        var error = new ErrorResultado(ErrorUsuarios.IntervaloInvitacion, 37);
        var fallo = Resultado<string?>.Fallido(error);

        Assert.True(ausencia.EsExitoso);
        Assert.Null(ausencia.Valor);
        Assert.False(fallo.EsExitoso);
        Assert.Equal(error, fallo.Error);
    }
}
