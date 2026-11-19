using AutoTallerPro.Frontend.ApiUsuarios;
using AutoTallerPro.Frontend.Seguridad;

var builder = WebApplication.CreateBuilder(args);

builder.AddSesionKeycloak();
builder.Services.AddRazorPages(opciones =>
{
    opciones.Conventions.AuthorizeFolder("/Administracion/Usuarios", SesionKeycloak.SoloAdministrador);
    opciones.Conventions.AllowAnonymousToPage("/Error");
    opciones.Conventions.AllowAnonymousToPage("/Privacy");
    opciones.Conventions.AllowAnonymousToPage("/Cuenta/ErrorSesion");
});

var gateway = builder.Configuration["GrupoUno:GatewayUrl"] ??
    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        ? "http://gateway:8080/"
        : "http://127.0.0.1:5100/");
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ClienteUsuarios>(cliente =>
{
    cliente.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
    cliente.Timeout = TimeSpan.FromSeconds(20);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
