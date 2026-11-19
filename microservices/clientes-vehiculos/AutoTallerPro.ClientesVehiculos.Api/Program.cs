using Npgsql;
using AutoTallerPro.ClientesVehiculos.Api.Adaptadores;
using AutoTallerPro.ClientesVehiculos.Api.Seguridad;
using AutoTallerPro.ClientesVehiculos.Application.CasosDeUso;
using AutoTallerPro.ClientesVehiculos.Application.Puertos;
using AutoTallerPro.ClientesVehiculos.Infrastructure.Persistencia;

var builder = WebApplication.CreateBuilder(args);
builder.AgregarSeguridad();
builder.Services.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
{
    if (contexto.ProblemDetails.Status >= 500)
    {
        contexto.ProblemDetails.Title = "No se pudo completar la operación.";
        contexto.ProblemDetails.Detail = null;
    }
});
builder.Services.AddSingleton(_ => new Conexion(new NpgsqlConnectionStringBuilder { Host = Requerido("Host"), Port = int.Parse(builder.Configuration["Database:Port"] ?? "5432"), Username = Requerido("User"), Password = Requerido("Password"), Database = Requerido("Name") }.ConnectionString));
builder.Services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
builder.Services.AddScoped<ClienteServicio>();
builder.Services.AddScoped<IVehiculoRepositorio, VehiculoRepositorio>();
builder.Services.AddScoped<VehiculoServicio>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/salud", () => Results.Ok(new { estado = "OK" })).AllowAnonymous();
app.MapearCliente();
app.MapearVehiculo();
app.Run();
string Requerido(string clave) => builder.Configuration[$"Database:{clave}"] ?? throw new InvalidOperationException($"Falta Database:{clave}.");
public partial class Program;
