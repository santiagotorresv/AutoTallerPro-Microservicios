using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using MySqlConnector;
using MongoDB.Driver;
using MongoDB.Bson;
using Clientes = AutoTallerPro.ClientesVehiculos;
using Catalogo = AutoTallerPro.Servicios;
using Personal = AutoTallerPro.Mecanicos;
namespace AutoTallerPro.GrupoUno.Tests;

public sealed class PersistenciaFactAttribute : FactAttribute
{
    public PersistenciaFactAttribute()
    { if (Environment.GetEnvironmentVariable("GRUPO1_PRUEBAS_AISLADAS") != "1") Skip = "Ejecute scripts/grupo-1/verificar_persistencia.py para crear recursos aislados."; }
}
public sealed class PersistenciaTests
{
    private static string Variable(string nombre) => Environment.GetEnvironmentVariable(nombre) ?? throw new InvalidOperationException($"Falta {nombre}.");
    private static void VerificarBase(string nombre)
    { if (!nombre.StartsWith("test_grupo1_", StringComparison.Ordinal)) throw new InvalidOperationException("Las pruebas solo admiten bases propias con prefijo test_grupo1_."); }
    [PersistenciaFact] public async Task PostgreSqlCrudRestriccionesYRefrescoReales()
    {
        var cadena = Variable("GRUPO1_POSTGRES"); VerificarBase(new NpgsqlConnectionStringBuilder(cadena).Database!);
        await using (var conexion = new NpgsqlConnection(cadena))
        {
            await conexion.OpenAsync();
            foreach (var archivo in new[] { "001_clientes_vehiculos.sql", "002_placa_extranjera.sql", "003_auditoria_clientes_vehiculos.sql" })
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", archivo));
                for (var i = 0; i < 2; i++) { await using var cmd = new NpgsqlCommand(sql, conexion); await cmd.ExecuteNonQueryAsync(); }
            }
        }
        var clientes = new Clientes.Infrastructure.Persistencia.ClienteRepositorio(new(cadena));
        var vehiculos = new Clientes.Infrastructure.Persistencia.VehiculoRepositorio(new(cadena));
        var s = new Clientes.Application.CasosDeUso.ClienteServicio(clientes);
        var v = new Clientes.Application.CasosDeUso.VehiculoServicio(vehiculos, clientes);
        var documento = Random.Shared.Next(1000000, 9999999).ToString(); var documentoHttp = Random.Shared.Next(10000000, 99999999).ToString();
        var cliente = (await s.GuardarAsync(null, new(documento, "Cliente persistido", "71234567", null), default)).Valor;
        var vehiculo = (await v.GuardarAsync(null, new(cliente.Id, "1234-ABC", "Toyota", "Corolla", 2020, null), default)).Valor;
        Assert.Equal("1234ABC", vehiculo.Placa);
        Assert.Equal(Clientes.Domain.Resultados.TipoError.Conflicto, (await s.EliminarAsync(cliente.Id, default)).Error!.Tipo);
        Assert.True((await v.GuardarAsync(vehiculo.Id, new(cliente.Id, "1234-ABC", "Toyota", "Yaris", 2021, "Azul"), default)).EsExitoso);
        Assert.Equal("Yaris", (await new Clientes.Infrastructure.Persistencia.VehiculoRepositorio(new(cadena)).ObtenerAsync(vehiculo.Id, default))!.Modelo);
        var duplicado = Clientes.Domain.Cliente.Crear(Guid.NewGuid(), documento.ToLowerInvariant(), "Duplicado", "71234567", null, true).Valor;
        Assert.Equal(Clientes.Domain.Resultados.TipoError.Conflicto, (await clientes.GuardarAsync(duplicado, true, default)).Error!.Tipo);
        var placaDuplicada = Clientes.Domain.Vehiculo.Crear(Guid.NewGuid(), cliente.Id, "1234ABC", "Toyota", "Corolla", 2020, null, true).Valor;
        Assert.Equal(Clientes.Domain.Resultados.TipoError.Conflicto, (await vehiculos.GuardarAsync(placaDuplicada, true, default)).Error!.Tipo);
        var huerfano = Clientes.Domain.Vehiculo.Crear(Guid.NewGuid(), Guid.NewGuid(), "9999XYZ", "Toyota", "Corolla", 2020, null, true).Valor;
        Assert.False((await vehiculos.GuardarAsync(huerfano, true, default)).EsExitoso);
        var antes = (await vehiculos.ListarAsync(default)).Count;
        Assert.False((await v.GuardarAsync(null, new(cliente.Id, "!", "Toyota", "Corolla", 1800, null), default)).EsExitoso);
        Assert.Equal(antes, (await vehiculos.ListarAsync(default)).Count);
        Assert.True((await s.GuardarAsync(cliente.Id, new(documento, "Cliente actualizado", "71234567", null), default)).EsExitoso);
        Assert.Equal("Cliente actualizado", (await s.ListarAsync(documento, "nombre", false, default)).Valor.Single().Nombre);
        void Configurar(Microsoft.Extensions.DependencyInjection.IServiceCollection registros)
        {
            registros.RemoveAll<Clientes.Application.Puertos.IClienteRepositorio>(); registros.AddSingleton<Clientes.Application.Puertos.IClienteRepositorio>(clientes);
            registros.RemoveAll<Clientes.Application.Puertos.IVehiculoRepositorio>(); registros.AddSingleton<Clientes.Application.Puertos.IVehiculoRepositorio>(vehiculos);
        }
        await FlujoHttpPersistencia.Verificar<Clientes.Api.ApiAssembly>("/clientes", Configurar,
            new Clientes.Application.DatosCliente(documentoHttp, "Cliente HTTP", "71234567", null), new Clientes.Application.DatosCliente(documentoHttp, "Cliente HTTP actualizado", "71234567", null),
            new Clientes.Application.DatosCliente("", "", null, null), "nombre", "Cliente HTTP actualizado");
        await FlujoHttpPersistencia.Verificar<Clientes.Api.ApiAssembly>("/vehiculos", Configurar,
            new Clientes.Application.DatosVehiculo(cliente.Id, "7777XYZ", "Nissan", "March", 2021, null), new Clientes.Application.DatosVehiculo(cliente.Id, "7777XYZ", "Nissan", "Versa", 2022, null),
            new Clientes.Application.DatosVehiculo(cliente.Id, "!", "Nissan", "March", 1800, null), "modelo", "Versa");
        Assert.True((await v.EliminarAsync(vehiculo.Id, default)).EsExitoso);
        Assert.True((await s.EliminarAsync(cliente.Id, default)).EsExitoso);
        Assert.Null(await clientes.ObtenerAsync(cliente.Id, default));
    }
    [PersistenciaFact] public async Task MySqlCrudRestriccionesYRefrescoReales()
    {
        var cadena = Variable("GRUPO1_MYSQL"); VerificarBase(new MySqlConnectionStringBuilder(cadena).Database);
        // Los scripts de esquema usan variables de sesión (@sql); el conector las admite solo con AllowUserVariables.
        await using (var conexion = new MySqlConnection(new MySqlConnectionStringBuilder(cadena) { AllowUserVariables = true }.ConnectionString))
        {
            await conexion.OpenAsync();
            foreach (var archivo in new[] { "001_catalogo_servicios.sql", "002_auditoria_catalogo_servicios.sql" })
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", archivo));
                for (var i = 0; i < 2; i++) { await using var cmd = new MySqlCommand(sql, conexion); await cmd.ExecuteNonQueryAsync(); }
            }
        }
        var categorias = new Catalogo.Infrastructure.Persistencia.CategoriaServicioRepositorio(new(cadena));
        var servicios = new Catalogo.Infrastructure.Persistencia.ServicioRepositorio(new(cadena));
        var c = new Catalogo.Application.CasosDeUso.CategoriaServicioServicio(categorias);
        var s = new Catalogo.Application.CasosDeUso.ServicioServicio(servicios, categorias);
        var nombre = "Categoría " + Guid.NewGuid().ToString("N")[..10];
        var categoria = (await c.GuardarAsync(null, new(nombre, null), default)).Valor;
        var servicio = (await s.GuardarAsync(null, new(categoria.Id, "Servicio " + Guid.NewGuid().ToString("N")[..10], null, 123.45m, 30), default)).Valor;
        Assert.Equal(Catalogo.Domain.Resultados.TipoError.Conflicto, (await c.EliminarAsync(categoria.Id, default)).Error!.Tipo);
        Assert.True((await s.GuardarAsync(servicio.Id, new(categoria.Id, servicio.Nombre, "Actualizado", 222.22m, 60), default)).EsExitoso);
        Assert.Equal(222.22m, (await new Catalogo.Infrastructure.Persistencia.ServicioRepositorio(new(cadena)).ObtenerAsync(servicio.Id, default))!.PrecioBase);
        Assert.True((await c.GuardarAsync(categoria.Id, new(nombre, "Revisada"), default)).EsExitoso);
        var duplicada = Catalogo.Domain.CategoriaServicio.Crear(Guid.NewGuid(), nombre.ToUpperInvariant(), null, true).Valor;
        Assert.Equal(Catalogo.Domain.Resultados.TipoError.Conflicto, (await categorias.GuardarAsync(duplicada, true, default)).Error!.Tipo);
        var duplicado = Catalogo.Domain.Servicio.Crear(Guid.NewGuid(), categoria.Id, servicio.Nombre.ToUpperInvariant(), null, 10, null, true).Valor;
        Assert.Equal(Catalogo.Domain.Resultados.TipoError.Conflicto, (await servicios.GuardarAsync(duplicado, true, default)).Error!.Tipo);
        var huerfano = Catalogo.Domain.Servicio.Crear(Guid.NewGuid(), Guid.NewGuid(), "Huérfano", null, 10, null, true).Valor;
        Assert.False((await servicios.GuardarAsync(huerfano, true, default)).EsExitoso);
        var antes = (await servicios.ListarAsync(default)).Count;
        Assert.False((await s.GuardarAsync(null, new(categoria.Id, "Inválido", null, -1, 30), default)).EsExitoso);
        Assert.Equal(antes, (await servicios.ListarAsync(default)).Count);
        Assert.Single((await s.ListarAsync(servicio.Nombre, "preciobase", true, default)).Valor);
        void Configurar(Microsoft.Extensions.DependencyInjection.IServiceCollection registros)
        {
            registros.RemoveAll<Catalogo.Application.Puertos.ICategoriaServicioRepositorio>(); registros.AddSingleton<Catalogo.Application.Puertos.ICategoriaServicioRepositorio>(categorias);
            registros.RemoveAll<Catalogo.Application.Puertos.IServicioRepositorio>(); registros.AddSingleton<Catalogo.Application.Puertos.IServicioRepositorio>(servicios);
        }
        await FlujoHttpPersistencia.Verificar<Catalogo.Api.ApiAssembly>("/categorias", Configurar,
            new Catalogo.Application.DatosCategoriaServicio("HTTP " + nombre, null), new Catalogo.Application.DatosCategoriaServicio("HTTP " + nombre, "Actualizada"),
            new Catalogo.Application.DatosCategoriaServicio("", null), "descripcion", "Actualizada");
        await FlujoHttpPersistencia.Verificar<Catalogo.Api.ApiAssembly>("/servicios", Configurar,
            new Catalogo.Application.DatosServicio(categoria.Id, "Servicio HTTP", null, 100, 30), new Catalogo.Application.DatosServicio(categoria.Id, "Servicio HTTP", "Actualizado", 200, 40),
            new Catalogo.Application.DatosServicio(categoria.Id, "", null, -1, 0), "descripcion", "Actualizado");
        Assert.True((await s.EliminarAsync(servicio.Id, default)).EsExitoso);
        Assert.True((await c.EliminarAsync(categoria.Id, default)).EsExitoso);
        Assert.Null(await servicios.ObtenerAsync(servicio.Id, default));
    }
    [PersistenciaFact] public async Task MongoCrudIndiceUnicoYRefrescoReales()
    {
        var url = new MongoUrl(Variable("GRUPO1_MONGO")); VerificarBase(url.DatabaseName);
        var db = new MongoClient(url).GetDatabase(url.DatabaseName);
        var repositorio = new Personal.Infrastructure.Persistencia.MecanicoRepositorio(db);
        var servicio = new Personal.Application.CasosDeUso.MecanicoServicio(repositorio);
        var documento = Random.Shared.Next(1000000, 9999999).ToString(); var documentoHttp = Random.Shared.Next(10000000, 99999999).ToString();
        var creado = (await servicio.GuardarAsync(null, new("Ana", "Pérez", documento, "71234567", null, "Electricidad"), default)).Valor;
        Assert.True((await servicio.GuardarAsync(creado.Id, new("Ana", "López", documento, "71234567", null, "Diagnóstico"), default)).EsExitoso);
        Assert.Equal("López", (await new Personal.Infrastructure.Persistencia.MecanicoRepositorio(db).ObtenerAsync(creado.Id, default))!.Apellidos);
        var duplicado = Personal.Domain.Mecanico.Crear(MongoDB.Bson.ObjectId.GenerateNewId().ToString(), "Ana", "Pérez", documento, "71234567", null, "Electricidad", true).Valor;
        Assert.Equal(Personal.Domain.Resultados.TipoError.Conflicto, (await repositorio.GuardarAsync(duplicado, true, default)).Error!.Tipo);
        var antes = (await repositorio.ListarAsync(default)).Count;
        Assert.False((await servicio.GuardarAsync(null, new("", "Pérez", "99999", null, null, "Electricidad"), default)).EsExitoso);
        Assert.Equal(antes, (await repositorio.ListarAsync(default)).Count);
        Assert.Single((await servicio.ListarAsync(documento, "apellidos", false, default)).Valor);
        void Configurar(Microsoft.Extensions.DependencyInjection.IServiceCollection registros)
        {
            registros.RemoveAll<Personal.Application.Puertos.IMecanicoRepositorio>(); registros.AddSingleton<Personal.Application.Puertos.IMecanicoRepositorio>(repositorio);
        }
        await FlujoHttpPersistencia.Verificar<Personal.Api.ApiAssembly>("/mecanicos", Configurar,
            new Personal.Application.DatosMecanico("Ana", "HTTP", documentoHttp, "71234567", null, "Electricidad"),
            new Personal.Application.DatosMecanico("Ana", "HTTP actualizada", documentoHttp, "71234567", null, "Diagnóstico"),
            new Personal.Application.DatosMecanico("", "HTTP", "INVALIDO", null, null, "Otra"), "apellidos", "HTTP actualizada");
        Assert.True((await servicio.EliminarAsync(creado.Id, default)).EsExitoso);
        Assert.Null(await repositorio.ObtenerAsync(creado.Id, default));
    }
}
