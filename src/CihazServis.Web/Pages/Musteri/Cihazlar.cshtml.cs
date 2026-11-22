using CihazServis.Application.Servis;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Musteri;

public class CihazlarModel(MusteriCihazServisi servis) : PageModel
{
    public List<MusteriCihazi> Cihazlar { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (User.KurumId() is not { } kurum)
        {
            return Forbid();
        }

        Cihazlar = await servis.CihazlarAsync(kurum, ct);
        return Page();
    }
}
