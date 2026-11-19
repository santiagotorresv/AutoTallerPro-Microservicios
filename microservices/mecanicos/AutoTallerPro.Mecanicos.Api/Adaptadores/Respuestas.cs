using AutoTallerPro.Mecanicos.Domain.Resultados;
namespace AutoTallerPro.Mecanicos.Api.Adaptadores;
internal static class Respuestas
{
    internal static IResult Http<T>(Resultado<T> resultado, string? ubicacion = null)
    {
        if (resultado.EsExitoso) return ubicacion is null ? Results.Ok(resultado.Valor) : Results.Created(ubicacion, resultado.Valor);
        var error = resultado.Error!;
        var estado = error.Tipo switch { TipoError.NoEncontrado => 404, TipoError.Conflicto => 409, _ => 400 };
        return Results.Problem(statusCode: estado, title: error.Mensaje, extensions: new Dictionary<string, object?> { ["codigo"] = error.Codigo });
    }
}
