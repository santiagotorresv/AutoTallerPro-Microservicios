using AutoTallerPro.Servicios.Application;
using AutoTallerPro.Servicios.Application.CasosDeUso;
using AutoTallerPro.Servicios.Domain;
using AutoTallerPro.Servicios.Domain.Resultados;
namespace AutoTallerPro.GrupoUno.Tests;
public sealed class ServiciosTests
{
    private readonly CategoriaServicioMemoria categorias = new();
    private readonly ServicioMemoria servicios = new();
    private CategoriaServicioServicio Categorias => new(categorias);
    private ServicioServicio Servicio => new(servicios, categorias);
    private async Task<Guid> Categoria() => (await Categorias.GuardarAsync(null, new("Mantenimiento", null), default)).Valor.Id;
    [Fact] public async Task CategoriaValidaPersiste() { Assert.True((await Categorias.GuardarAsync(null, new("Mantenimiento", "Preventivo"), default)).EsExitoso); Assert.Single(categorias.Datos); }
    [Fact] public async Task CategoriaInvalidaNoEscribe() { Assert.False((await Categorias.GuardarAsync(null, new(" ", null), default)).EsExitoso); Assert.Equal(0, categorias.Escrituras); }
    [Fact] public async Task CategoriaDuplicadaEsConflicto() { await Categoria(); var r = await Categorias.GuardarAsync(null, new("mantenimiento", null), default); Assert.Equal(TipoError.Conflicto, r.Error!.Tipo); Assert.Equal(1, categorias.Escrituras); }
    [Fact] public async Task CrearServicioValidoPersiste() { var id = await Categoria(); Assert.True((await Servicio.GuardarAsync(null, new(id, "Cambio de aceite", null, 120.50m, 30), default)).EsExitoso); Assert.Single(servicios.Datos); }
    [Theory] [InlineData(-1, 30)] [InlineData(100, 0)] [InlineData(100, -1)] [InlineData(100, 10081)]
    public async Task ServicioInvalidoNoEscribe(decimal precio, int duracion) { var id = await Categoria(); Assert.False((await Servicio.GuardarAsync(null, new(id, "Cambio de aceite", null, precio, duracion), default)).EsExitoso); Assert.Equal(0, servicios.Escrituras); }
    [Fact] public void PrecioNoSeRedondeaSilenciosamente() { Assert.False(AutoTallerPro.Servicios.Domain.Servicio.Crear(Guid.NewGuid(), Guid.NewGuid(), "Lavado", null, 1.234m, null, true).EsExitoso); }
    [Fact] public async Task CategoriaAusenteNoEscribe() { var r = await Servicio.GuardarAsync(null, new(Guid.NewGuid(), "Lavado", null, 10, null), default); Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo); Assert.Empty(servicios.Datos); }
    [Fact] public async Task ServicioDuplicadoNoEscribe() { var id = await Categoria(); await Servicio.GuardarAsync(null, new(id, "Lavado", null, 10, null), default); var r = await Servicio.GuardarAsync(null, new(id, "LAVADO", null, 10, null), default); Assert.Equal(TipoError.Conflicto, r.Error!.Tipo); Assert.Equal(1, servicios.Escrituras); }
    [Fact] public async Task ActualizarValidoPersiste() { var id = await Categoria(); var creado = (await Servicio.GuardarAsync(null, new(id, "Lavado", null, 10, null), default)).Valor; var r = await Servicio.GuardarAsync(creado.Id, new(id, "Lavado completo", null, 20, 40), default); Assert.True(r.EsExitoso); Assert.Equal(20, servicios.Datos[creado.Id].PrecioBase); }
    [Fact] public async Task ActualizarInvalidoNoModifica() { var id = await Categoria(); var creado = (await Servicio.GuardarAsync(null, new(id, "Lavado", null, 10, null), default)).Valor; Assert.False((await Servicio.GuardarAsync(creado.Id, new(id, "", null, 10, null), default)).EsExitoso); Assert.Equal(1, servicios.Escrituras); }
    [Fact] public async Task AusenteDevuelveNoEncontrado() { var r = await Servicio.ObtenerAsync(Guid.NewGuid(), default); Assert.Equal(TipoError.NoEncontrado, r.Error!.Tipo); }
    [Fact] public async Task EliminarValidoRetiraRegistro() { var id = await Categoria(); var creado = (await Servicio.GuardarAsync(null, new(id, "Lavado", null, 10, null), default)).Valor; Assert.True((await Servicio.EliminarAsync(creado.Id, default)).EsExitoso); Assert.Empty(servicios.Datos); }
    [Fact] public async Task ListadoFiltraYOrdena() { var id = await Categoria(); await Servicio.GuardarAsync(null, new(id, "Lavado", null, 10, null), default); await Servicio.GuardarAsync(null, new(id, "Alineación", null, 20, null), default); var r = (await Servicio.ListarAsync(null, "nombre", false, default)).Valor; Assert.Equal("Alineación", r[0].Nombre); Assert.Single((await Servicio.ListarAsync("lav", null, true, default)).Valor); }
}
