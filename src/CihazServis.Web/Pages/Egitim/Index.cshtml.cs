using CihazServis.Application.Egitim;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Egitim;

public class IndexModel(EgitimServisi egitim) : PageModel
{
    public IReadOnlyList<KursDurumu> Kurslar { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
        => Kurslar = await egitim.KurslarAsync(User.KullaniciId()!.Value, ct);
}
