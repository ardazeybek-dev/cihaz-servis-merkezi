using CihazServis.Application.Egitim;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Egitim;

public class DersModel(EgitimServisi egitim) : PageModel
{
    public DersDurumu Ders { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var d = await egitim.DersDurumuAsync(User.KullaniciId()!.Value, id, ct);
        if (d is null)
        {
            return NotFound();
        }

        // Adresi değiştirerek kilitli derse geçilemez.
        if (d.Value.Durum.Asama == DersAsamasi.Kilitli)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        Ders = d.Value.Durum;
        return Page();
    }
}
