using AutoTallerPro.Usuarios.Application.Servicios;
using AutoTallerPro.Usuarios.Application.Validaciones;

namespace AutoTallerPro.Usuarios.Tests;

public sealed class GeneradorNombreUsuarioServiceTests
{
    [Theory]
    [InlineData("Juan.Pérez@empresa.com", "juan.perez")]
    [InlineData("MARÍA_GONZÁLEZ@correo.com", "maria_gonzalez")]
    [InlineData("user@domain.com", "user")]
    [InlineData("Ana++Lopez@empresa.com", "ana-lopez")]
    [InlineData("Ana東京Lopez@empresa.com", "ana-lopez")]
    [InlineData("A🙂B@empresa.com", "a-b")]
    [InlineData("東京@empresa.com", "usuario")]
    [InlineData("Ío@empresa.com", "usuario")]
    public void GenerarCandidatos_NormalizaYEmiteSoloUsuariosValidos(string correo, string esperado)
    {
        var candidatos = GeneradorNombreUsuarioService.GenerarCandidatos(correo).ToArray();

        Assert.Equal(esperado, candidatos[0]);
        Assert.Equal(esperado + "-1", candidatos[1]);
        Assert.Equal(esperado + "-100", candidatos[^1]);
        Assert.All(candidatos, candidato => Assert.True(ReglasCuenta.UsuarioValido(candidato)));
    }

    [Fact]
    public void GenerarCandidatos_ReservaEspacioParaCadaSufijo()
    {
        var candidatos = GeneradorNombreUsuarioService
            .GenerarCandidatos(new string('a', 60) + "@empresa.com").ToArray();

        Assert.Equal(101, candidatos.Length);
        Assert.Equal(new string('a', 50), candidatos[0]);
        Assert.Equal(new string('a', 48) + "-1", candidatos[1]);
        Assert.Equal(new string('a', 47) + "-10", candidatos[10]);
        Assert.Equal(new string('a', 46) + "-100", candidatos[100]);
        Assert.All(candidatos, candidato => Assert.True(ReglasCuenta.UsuarioValido(candidato)));
    }

    [Fact]
    public async Task GenerarUsuarioUnicoAsync_DevuelveElPrimerCandidatoDisponible()
    {
        var ocupados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ana.lopez", "ANA.LOPEZ-1" };

        var candidato = await GeneradorNombreUsuarioService.GenerarUsuarioUnicoAsync(
            "Ana.Lopez@empresa.com", (username, _) => Task.FromResult(ocupados.Contains(username)));

        Assert.Equal("ana.lopez-2", candidato);
    }

    [Fact]
    public async Task GenerarUsuarioUnicoAsync_PermiteElUltimoSufijo()
    {
        var candidato = await GeneradorNombreUsuarioService.GenerarUsuarioUnicoAsync(
            "ana@empresa.com", (username, _) => Task.FromResult(username != "ana-100"));

        Assert.Equal("ana-100", candidato);
    }

    [Fact]
    public async Task GenerarUsuarioUnicoAsync_FallaTrasAgotarLosCandidatos()
    {
        var intentos = new List<string>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GeneradorNombreUsuarioService.GenerarUsuarioUnicoAsync("ana@empresa.com", (username, _) =>
            {
                intentos.Add(username);
                return Task.FromResult(true);
            }));

        Assert.Equal(101, intentos.Count);
        Assert.Equal("ana-100", intentos[^1]);
    }

    [Fact]
    public async Task GenerarUsuarioUnicoAsync_RechazaCorreoVacio()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            GeneradorNombreUsuarioService.GenerarUsuarioUnicoAsync("", (_, _) => Task.FromResult(false)));
    }

    [Fact]
    public async Task GenerarUsuarioUnicoAsync_RespetaCancelacionAntesDeConsultarDisponibilidad()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            GeneradorNombreUsuarioService.GenerarUsuarioUnicoAsync(
                "ana@empresa.com", (_, _) => throw new InvalidOperationException(), cancelacion.Token));
    }
}
