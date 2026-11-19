using MySqlConnector;
using AutoTallerPro.Servicios.Api.Adaptadores;
using AutoTallerPro.Servicios.Api.Seguridad;
using AutoTallerPro.Servicios.Application.CasosDeUso;
using AutoTallerPro.Servicios.Application.Puertos;
using AutoTallerPro.Servicios.Infrastructure.Persistencia;

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
builder.Services.AddSingleton(_ => new Conexion(new MySqlConnectionStringBuilder { Server = Requerido("Host"), Port = uint.Parse(builder.Configuration["Database:Port"] ?? "3306"), UserID = Requerido("User"), Password = Requerido("Password"), Database = Requerido("Name") }.ConnectionString));
builder.Services.AddScoped<ICategoriaServicioRepositorio, CategoriaServicioRepositorio>();
builder.Services.AddScoped<CategoriaServicioServicio>();
builder.Services.AddScoped<IServicioRepositorio, ServicioRepositorio>();
builder.Services.AddScoped<ServicioServicio>();
var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/salud", () => Results.Ok(new { estado = "OK" })).AllowAnonymous();
app.MapearCategoriaServicio();
app.MapearServicio();
app.Run();
string Requerido(string clave) => builder.Configuration[$"Database:{clave}"] ?? throw new InvalidOperationException($"Falta Database:{clave}.");
public partial class Program;
