using CihazServis.Application.Ortak;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Web.Pages.Yonetim;

public sealed record DenetimSatiri(DateTimeOffset Zaman, string? Kullanici, string Islem, string Varlik, string? VarlikNo, string? Ayrinti);

/// <summary>Yalnız yönetici görür. Tablo tetikleyiciyle korunur: kayıtlar değiştirilemez, silinemez.</summary>
[Authorize(Policy = Politikalar.Yonetici)]
public class DenetimModel(IUygulamaDbContext db) : PageModel
{
    public List<DenetimSatiri> Kayitlar { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Kayitlar = await db.DenetimKayitlari.AsNoTracking()
            .OrderByDescending(d => d.Id).Take(300)
            .Select(d => new DenetimSatiri(d.Zaman,
                db.Kullanicilar.Where(k => k.Id == d.KullaniciId).Select(k => k.AdSoyad).FirstOrDefault(),
                d.Islem, d.Varlik, d.VarlikNo, d.Ayrinti))
            .ToListAsync(ct);
    }
}
