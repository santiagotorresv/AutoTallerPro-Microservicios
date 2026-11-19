using AutoTallerPro.Mecanicos.Application;
using AutoTallerPro.Mecanicos.Application.CasosDeUso;
using AutoTallerPro.Mecanicos.Domain.Resultados;
namespace AutoTallerPro.GrupoUno.Tests;
public sealed class MecanicosTests
{
    private readonly MecanicoMemoria memoria = new();
    private MecanicoServicio Servicio => new(memoria);
    private static DatosMecanico Valido(string documento = "1234567", string apellidos = "Pérez") => new("Ana", apellidos, documento, "+59176543210", "ana@example.com", "Mecánica general");
    [Fact] public async Task CrearValidoPersisteConIdMongo() { var r = await Servicio.GuardarAsync(null, Valido(), default); Assert.True(r.EsExitoso); Assert.Matches("^[0-9a-f]{24}$", r.Valor.Id); Assert.Single(memoria.Datos); }
    [Theory] [InlineData("", "Pérez", "1234", null, null, "Mecánica general")] [InlineData("Ana", "", "1234", null, null, "Mecánica general")] [InlineData("Ana", "Pérez", "!", null, null, "Mecánica general")] [InlineData("Ana", "Pérez", "1234", "1", null, "Mecánica general")] [InlineData("Ana", "Pérez", "1234", null, "invalido", "Mecánica general")] [InlineData("Ana", "Pérez", "1234", null, null, "Otra")]
    public async Task CrearInvalidoNoEscribe(string nombres, string apellidos, string documento, string? telefono, string? correo, string especialidad) { var r = await Servicio.GuardarAsync(null, new(nombres, apellidos, documento, telefono, correo, especialidad), default); Assert.False(r.EsExitoso); Assert.Equal(TipoError.Validacion, r.Error!.Tipo); Assert.Equal(0, memoria.Escrituras); }
    [Fact] public async Task DuplicadoEsConflicto() { await Servicio.GuardarAsync(null, Valido(), default); var r = await Servicio.GuardarAsync(null, Valido(), default); Assert.Equal(TipoError.Conflicto, r.Error!.Tipo); Assert.Equal(1, memoria.Escrituras); }
    [Fact] public async Task ActualizarValidoConservaRegistro() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; var r = await Servicio.GuardarAsync(creado.Id, Valido(apellidos: "López"), default); Assert.True(r.EsExitoso); Assert.Equal("López", memoria.Datos[creado.Id].Apellidos); Assert.Equal(creado.FechaRegistro, r.Valor.FechaRegistro); }
    [Fact] public async Task ActualizarInvalidoNoModifica() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; Assert.False((await Servicio.GuardarAsync(creado.Id, Valido() with { Nombres = "" }, default)).EsExitoso); Assert.Equal(1, memoria.Escrituras); }
    [Fact] public async Task AusenteDevuelveNoEncontrado() { var r = await Servicio.ObtenerAsync("0123456789abcdef01234567", default); Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo); }
    [Theory] [InlineData("invalido")] [InlineData("")] [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task IdInvalidoEsErrorControlado(string id) { var r = await Servicio.ObtenerAsync(id, default); Assert.Equal(TipoError.Validacion, r.Error!.Tipo); Assert.False((await Servicio.EliminarAsync(id, default)).EsExitoso); }
    [Fact] public async Task EliminarValidoRetiraRegistro() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; Assert.True((await Servicio.EliminarAsync(creado.Id, default)).EsExitoso); Assert.Empty(memoria.Datos); }
    [Fact] public async Task ListadoOrdenaYBusca() { await Servicio.GuardarAsync(null, Valido("1111", "Zapata"), default); await Servicio.GuardarAsync(null, Valido("2222", "Arias"), default); Assert.Equal("Arias", (await Servicio.ListarAsync(null, "apellidos", false, default)).Valor[0].Apellidos); Assert.Equal("Zapata", (await Servicio.ListarAsync(null, "apellidos", true, default)).Valor[0].Apellidos); Assert.Single((await Servicio.ListarAsync("1111", null, false, default)).Valor); }
}
