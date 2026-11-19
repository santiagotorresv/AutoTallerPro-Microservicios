using MongoDB.Driver;
using AutoTallerPro.Mecanicos.Api.Adaptadores;
using AutoTallerPro.Mecanicos.Api.Seguridad;
using AutoTallerPro.Mecanicos.Application.CasosDeUso;
using AutoTallerPro.Mecanicos.Application.Puertos;
using AutoTallerPro.Mecanicos.Infrastructure.Persistencia;
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
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(new MongoClientSettings
{
    Server = new MongoServerAddress(Requerido("Host"), int.Parse(builder.Configuration["Database:Port"] ?? "27017")),
    Credential = MongoCredential.CreateCredential(builder.Configuration["Database:AuthSource"] ?? Requerido("Name"), Requerido("User"), Requerido("Password")),
    ServerSelectionTimeout = TimeSpan.FromSeconds(10)
}));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(Requerido("Name")));
builder.Services.AddScoped<IMecanicoRepositorio, MecanicoRepositorio>();
builder.Services.AddScoped<MecanicoServicio>();
var app = builder.Build();
app.UseExceptionHandler(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/salud", () => Results.Ok(new { estado = "OK" })).AllowAnonymous();
app.MapearMecanico();
app.Run();
string Requerido(string clave) => builder.Configuration[$"Database:{clave}"] ?? throw new InvalidOperationException($"Falta Database:{clave}.");
public partial class Program;
