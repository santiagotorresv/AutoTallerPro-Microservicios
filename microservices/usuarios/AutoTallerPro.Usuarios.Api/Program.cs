using System.Security.Claims;
using AutoTallerPro.Usuarios.Api.Persistencia;
using AutoTallerPro.Usuarios.Api.Seguridad;
using AutoTallerPro.Usuarios.Api.Usuarios;
using AutoTallerPro.Usuarios.Application.Identidad;
using AutoTallerPro.Usuarios.Application.Servicios;
using Microsoft.IdentityModel.JsonWebTokens;

bool verificarMySql = args is ["--verificar-mysql"];
bool verificarKeycloak = args is ["--verificar-keycloak-admin"];
var builder = WebApplication.CreateBuilder(verificarMySql || verificarKeycloak ? [] : args);
builder.AddAutenticacionUsuarios();
builder.AddPersistenciaUsuarios();
builder.AddKeycloakAdmin();
builder.Services.AddScoped<AltaUsuariosServicio>();
builder.Services.AddScoped<ConsultaUsuariosServicio>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemasUsuarios>();

var app = builder.Build();

if (verificarMySql)
{
    await using var aplicacion = app;
    await ConfiguracionPersistenciaUsuarios.VerificarConexionAsync(aplicacion.Services);
    Console.WriteLine("Conexión a MySQL de Usuarios verificada.");
    return;
}

if (verificarKeycloak)
{
    await using var aplicacion = app;
    var identidades = aplicacion.Services.GetRequiredService<IIdentidadesKeycloak>();
    await identidades.ExisteUsernameAsync($"verificacion-{Guid.NewGuid():N}");
    Console.WriteLine("Conexión técnica y consulta de Usuarios verificadas en Keycloak.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => Results.Ok(new { estado = "OK" })).AllowAnonymous();

app.MapGet("/yo", (ClaimsPrincipal usuario) => Results.Ok(new
{
    id = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub),
    username = usuario.Identity?.Name,
    roles = usuario.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray()
})).RequireAuthorization(AutenticacionUsuarios.PersonalAutorizado);

app.MapUsuarios();
app.Run();

public partial class Program { }
