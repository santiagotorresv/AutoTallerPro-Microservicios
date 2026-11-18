var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/salud", () => Results.Ok(new { estado = "OK" }));

app.Run();
