using CihazServis.Application.Servis;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Musteri;

/// <summary>Başka kurumun cihazı 403 değil 404 döner: kimliğin var olduğu da sızmaz.</summary>
public class CihazModel(MusteriCihazServisi servis) : PageModel
{
    public MusteriCihazDetayi? Detay { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Detay = User.KurumId() is { } kurum ? await servis.CihazAsync(kurum, id, ct) : null;
        return Detay is null ? NotFound() : Page();
    }
}
