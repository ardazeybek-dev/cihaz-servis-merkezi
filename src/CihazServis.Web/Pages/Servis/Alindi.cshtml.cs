using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Servis;

/// <summary>Formdan sonra yönlendirilen sayfa: yenilemek ikinci bir talep açmaz, numara adreste görünmez.</summary>
public class AlindiModel : PageModel
{
    public string? No { get; private set; }

    public DateOnly? HedefTarih { get; private set; }

    public IActionResult OnGet()
    {
        Response.Headers.CacheControl = "no-store";
        if (TempData["TalepNo"] is not string no)
        {
            return RedirectToPage("/Servis/Talep");
        }

        No = no;
        HedefTarih = TempData["HedefTarih"] is string h && DateOnly.TryParse(h, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : null;
        return Page();
    }
}
