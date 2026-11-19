using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace AutoTallerPro.Frontend.GrupoUno;

public sealed class DecimalMonedaBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext contexto)
    {
        var entrada = contexto.ValueProvider.GetValue(contexto.ModelName);
        if (entrada == ValueProviderResult.None) return Task.CompletedTask;
        contexto.ModelState.SetModelValue(contexto.ModelName, entrada);
        var texto = entrada.FirstValue?.Trim().Replace(',', '.');
        if (decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var precio))
            contexto.Result = ModelBindingResult.Success(precio);
        else contexto.ModelState.TryAddModelError(contexto.ModelName, "Ingrese un precio válido, sin separadores de miles.");
        return Task.CompletedTask;
    }
}
