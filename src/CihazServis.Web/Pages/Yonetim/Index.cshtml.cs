using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Yonetim;

public class IndexModel(ServisTalebiServisi servis) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public TalepDurumu? Durum { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Kapalilar { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Benim { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Arama { get; set; }

    public List<TalepSatiri> Talepler { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Talepler = await servis.ListeleAsync(
            new TalepFiltresi(Durum, !Kapalilar, Benim ? User.KullaniciId() : null, Arama), ct);
    }
}
