using AutoTallerPro.ClientesVehiculos.Api.Persistencia;
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
builder.Services.AddSingleton(_ => new Conexion(ConfiguracionPersistencia.Cadena(builder.Configuration)));
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
public partial class Program;
