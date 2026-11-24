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
}).AddMvcOptions(opciones =>
{
    var mensajes = opciones.ModelBindingMessageProvider;
    mensajes.SetValueMustBeANumberAccessor(campo => $"{campo}: ingrese un número válido.");
    mensajes.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"{campo}: el valor «{valor}» no es válido.");
    mensajes.SetValueIsInvalidAccessor(valor => $"El valor «{valor}» no es válido.");
    mensajes.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    mensajes.SetMissingBindRequiredValueAccessor(campo => $"{campo}: este campo es obligatorio.");
    mensajes.SetUnknownValueIsInvalidAccessor(campo => $"{campo}: el valor no es válido.");
    mensajes.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor «{valor}» no es válido.");
    mensajes.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor no es válido.");
    mensajes.SetNonPropertyValueMustBeANumberAccessor(() => "Ingrese un número válido.");
});

var gateway = builder.Configuration["GrupoUno:GatewayUrl"] ??
    (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
        ? "http://gateway:8080/"
        : "http://127.0.0.1:5100/");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ReintentosUsuarios>();
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
