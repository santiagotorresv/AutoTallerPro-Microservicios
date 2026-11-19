using AutoTallerPro.ClientesVehiculos.Application;
using AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
using AutoTallerPro.ClientesVehiculos.Domain;
using AutoTallerPro.ClientesVehiculos.Domain.Resultados;
namespace AutoTallerPro.GrupoUno.Tests;
public sealed class ClientesVehiculosTests
{
    private readonly ClienteMemoria clientes = new();
    private ClienteServicio Servicio => new(clientes);
    private static DatosCliente Valido(string documento = "1234567", string nombre = "Ana Pérez") => new(documento, nombre, "+59176543210", "ana@example.com");
    [Fact] public async Task CrearValidoNormalizaYPersiste() { var r = await Servicio.GuardarAsync(null, Valido(" ab-123 "), default); Assert.True(r.EsExitoso); Assert.Equal("AB-123", r.Valor.Documento); Assert.Single(clientes.Datos); }
    [Theory]
    [InlineData("", "Ana", null, null)]
    [InlineData("1234", "", null, null)]
    [InlineData("1234", "Ana", "12", null)]
    [InlineData("1234", "Ana", null, "correo incorrecto")]
    public async Task CrearInvalidoNoEscribe(string doc, string nombre, string? telefono, string? correo)
    { var r = await Servicio.GuardarAsync(null, new(doc, nombre, telefono, correo), default); Assert.False(r.EsExitoso); Assert.Equal(TipoError.Validacion, r.Error!.Tipo); Assert.Equal(0, clientes.Escrituras); }
    [Fact] public async Task DocumentoDuplicadoEsConflicto() { await Servicio.GuardarAsync(null, Valido(), default); var r = await Servicio.GuardarAsync(null, Valido(), default); Assert.Equal(TipoError.Conflicto, r.Error!.Tipo); Assert.Equal(1, clientes.Escrituras); }
    [Fact] public async Task ActualizarConservaFechaRegistro() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; var r = await Servicio.GuardarAsync(creado.Id, Valido(nombre: "Ana López"), default); Assert.True(r.EsExitoso); Assert.Equal("Ana López", r.Valor.Nombre); Assert.Equal(creado.FechaRegistro, r.Valor.FechaRegistro); }
    [Fact] public async Task ActualizacionInvalidaNoModifica() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; var r = await Servicio.GuardarAsync(creado.Id, Valido(nombre: ""), default); Assert.False(r.EsExitoso); Assert.Equal("Ana Pérez", clientes.Datos[creado.Id].Nombre); Assert.Equal(1, clientes.Escrituras); }
    [Fact] public async Task AusenteDevuelveNoEncontrado() { var r = await Servicio.ObtenerAsync(Guid.NewGuid(), default); Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo); }
    [Fact] public async Task EliminarValidoRetiraRegistro() { var creado = (await Servicio.GuardarAsync(null, Valido(), default)).Valor; Assert.True((await Servicio.EliminarAsync(creado.Id, default)).EsExitoso); Assert.Empty(clientes.Datos); }
    [Fact] public async Task ListadoFiltraYOrdena() { await Servicio.GuardarAsync(null, Valido("1111", "Beatriz"), default); await Servicio.GuardarAsync(null, Valido("2222", "Ana"), default); var lista = (await Servicio.ListarAsync(null, "nombre", false, default)).Valor; Assert.Equal(new[] {"Ana", "Beatriz"}, lista.Select(x => x.Nombre)); Assert.Single((await Servicio.ListarAsync("ana", null, false, default)).Valor); Assert.Equal("Beatriz", (await Servicio.ListarAsync(null, "nombre", true, default)).Valor[0].Nombre); }
    [Fact] public async Task VehiculoSinPropietarioNoSeGuarda() { var memoria = new VehiculoMemoria(); var s = new VehiculoServicio(memoria, clientes); var r = await s.GuardarAsync(null, new(Guid.NewGuid(), "1234ABC", "Toyota", "Corolla", 2020, null), default); Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo); Assert.Equal(0, memoria.Escrituras); }
    [Theory] [InlineData("!", 2020)] [InlineData("1234ABC", 1800)] [InlineData("1234ABC", 9999)]
    public void VehiculoInvalidoSeRechaza(string placa, int anio) { Assert.False(Vehiculo.Crear(Guid.NewGuid(), Guid.NewGuid(), placa, "Toyota", "Corolla", anio, null, true).EsExitoso); }
    [Fact] public async Task VehiculoConPropietarioInactivoSeRechaza() { var cliente = (await Servicio.GuardarAsync(null, Valido() with { Activo = false }, default)).Valor; var memoria = new VehiculoMemoria(); var r = await new VehiculoServicio(memoria, clientes).GuardarAsync(null, new(cliente.Id, "1234ABC", "Toyota", "Corolla", 2020, null), default); Assert.Equal(TipoError.Conflicto, r.Error!.Tipo); Assert.Equal(0, memoria.Escrituras); }
    [Fact] public void ResultadoFallidoNoExponeValor() { var r = Resultado<Cliente>.Fallo("prueba", "Validación fallida."); Assert.Throws<InvalidOperationException>(() => r.Valor); }
}
