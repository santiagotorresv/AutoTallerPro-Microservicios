using System.Collections;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using Npgsql;
using Postgres = AutoTallerPro.ClientesVehiculos.Api.Persistencia.ConfiguracionPersistencia;
using Mongo = AutoTallerPro.Mecanicos.Api.Persistencia.ConfiguracionPersistencia;
namespace AutoTallerPro.GrupoUno.Tests;

public sealed class ConexionNubeTests
{
    private static IConfiguration Config(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(x => new KeyValuePair<string, string?>("Database:" + x.Clave, x.Valor))).Build();

    [Fact] public void PostgresRemotoTienePrioridadYVerificaElServidor()
    {
        var config = Config(("ConnectionString", "Host=remoto.example;Database=clientes;Username=app;Password=clave_ficticia"), ("Host", "local"));
        var opciones = new NpgsqlConnectionStringBuilder(Postgres.Cadena(config));
        Assert.Equal("remoto.example", opciones.Host);
        Assert.Equal("clientes", opciones.Database);
        Assert.Equal(SslMode.VerifyFull, opciones.SslMode);
    }

    [Theory] [InlineData("Disable")] [InlineData("Require")]
    public void PostgresRemotoNoDesactivaValidacionTls(string modo)
    {
        var config = Config(("ConnectionString", $"Host=remoto.example;Database=clientes;Username=app;SSL Mode={modo}"));
        Assert.Throws<InvalidOperationException>(() => Postgres.Cadena(config));
    }

    [Fact] public void ErrorDeConfiguracionPostgresNoExponeElSecreto()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Postgres.Cadena(Config(("ConnectionString", "postgresql://usuario:clave_ficticia@remoto.example/clientes"))));
        Assert.DoesNotContain("clave_ficticia", error.ToString());
    }

    [Fact] public void BasesLocalesConservanHostPuertoYAutenticacion()
    {
        var config = Config(("Host", "127.0.0.1"), ("Port", "23456"), ("Name", "local"), ("User", "app"), ("Password", "clave_ficticia"));
        var pg = new NpgsqlConnectionStringBuilder(Postgres.Cadena(config));
        Assert.Equal(23456, pg.Port);
        Assert.Equal("127.0.0.1", pg.Host);
        var mongo = Mongo.Opciones(config);
        Assert.Equal(23456, mongo.Server.Port);
        Assert.Equal("local", mongo.Credential.Source);
    }

    [Fact] public void MongoRemotoConservaOpcionesReplicaYAutenticacion()
    {
        var config = Config(("ConnectionString", "mongodb://app:clave_ficticia@remoto.example/mecanicos?tls=true&authSource=admin&replicaSet=atlas-prueba&retryWrites=true"));
        var opciones = Mongo.Opciones(config);
        Assert.True(opciones.UseTls);
        Assert.False(opciones.AllowInsecureTls);
        Assert.Equal("admin", opciones.Credential.Source);
        Assert.Equal("atlas-prueba", opciones.ReplicaSetName);
        Assert.True(opciones.RetryWrites);
        Assert.Equal(TimeSpan.FromSeconds(10), opciones.ServerSelectionTimeout);
    }

    [Theory]
    [InlineData("mongodb://app:clave_ficticia@remoto.example/mecanicos?tls=false")]
    [InlineData("mongodb://app:clave_ficticia@remoto.example/mecanicos?tls=true&tlsInsecure=true")]
    public void MongoRemotoRechazaConexionesInseguras(string cadena)
        => Assert.Throws<InvalidOperationException>(() => Mongo.Opciones(Config(("ConnectionString", cadena))));

    [Fact] public void MongoAceptaUriSrvSinCambiarElFormatoDeAtlas()
    {
        using var dns = SinConsultaDns();
        var opciones = Mongo.Opciones(Config(("ConnectionString", "mongodb+srv://app:clave_ficticia@cluster.example/mecanicos?retryWrites=true&w=majority")));
        Assert.Equal(ConnectionStringScheme.MongoDBPlusSrv, opciones.Scheme);
        Assert.True(opciones.UseTls);
        Assert.Equal("cluster.example", opciones.Server.Host);
    }

    // El driver consulta DNS en URIs mongodb+srv; se sustituye su resolvedor interno durante el test.
    private static IDisposable SinConsultaDns()
    {
        var ensamblado = typeof(MongoClientSettings).Assembly;
        var interfaz = ensamblado.GetType("MongoDB.Driver.Core.Misc.IDnsResolver")!;
        var tipoSrv = ensamblado.GetType("MongoDB.Driver.Core.Misc.SrvRecord")!;
        var tipoTxt = ensamblado.GetType("MongoDB.Driver.Core.Misc.TxtRecord")!;
        var campo = ensamblado.GetType("MongoDB.Driver.Core.Misc.DnsClientWrapper")!.GetField("__instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var crear = typeof(DispatchProxy).GetMethods().Single(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);
        var simulacro = crear.MakeGenericMethod(interfaz, typeof(ResolverDnsFalso)).Invoke(null, null)!;
        var falso = (ResolverDnsFalso)simulacro;
        falso.TipoSrv = tipoSrv;
        falso.TipoTxt = tipoTxt;
        var original = campo.GetValue(null);
        campo.SetValue(null, simulacro);
        return new RestaurarDns(campo, original);
    }

    private sealed class RestaurarDns(FieldInfo campo, object? original) : IDisposable
    {
        public void Dispose() => campo.SetValue(null, original);
    }

    private class ResolverDnsFalso : DispatchProxy
    {
        public Type TipoSrv { get; set; } = null!;
        public Type TipoTxt { get; set; } = null!;

        protected override object Invoke(MethodInfo? metodo, object?[]? argumentos)
        {
            var esSrv = metodo!.Name.Contains("Srv", StringComparison.Ordinal);
            var tipo = esSrv ? TipoSrv : TipoTxt;
            var registros = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(tipo))!;
            if (esSrv)
                registros.Add(Activator.CreateInstance(TipoSrv, new DnsEndPoint("cluster.example", 27017), TimeSpan.FromSeconds(30))!);
            if (metodo.ReturnType.IsGenericType && metodo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(metodo.ReturnType.GetGenericArguments()[0]).Invoke(null, new object[] { registros })!;
            return registros;
        }
    }
}
