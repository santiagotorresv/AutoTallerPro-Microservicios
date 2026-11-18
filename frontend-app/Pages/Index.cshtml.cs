using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AutoTallerPro.Frontend.Pages;

public class IndexModel : PageModel
{
    public string FechaActual { get; } = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
        DateTimeOffset.UtcNow, "America/La_Paz").ToString("dd/MM/yyyy");

    public void OnGet()
    {
    }
}
