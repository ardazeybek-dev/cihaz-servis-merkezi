using CihazServis.Application.Dogrulama;
using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Servis;

public sealed record MusteriCihazi(int Id, string SeriNo, string ModelAdi, DateOnly KurulumTarihi, DateOnly GarantiBitis, string HizmetSeviyesi);

public sealed record MusteriTalebi(string No, DateTimeOffset OlusturmaZamani, string Konu, TalepDurumu Durum, string DurumAdi);

public sealed record MusteriCihazDetayi(MusteriCihazi Cihaz, IReadOnlyList<MusteriTalebi> Talepler);

/// <summary>
/// Müşteri yalnız kendi kurumunun cihazlarını görür. Kurum filtresi sorgunun içindedir:
/// başka kurumun cihaz kimliği "yok" ile aynı sonucu verir, varlığı da sızmaz.
/// </summary>
public sealed class MusteriCihazServisi(IUygulamaDbContext db)
{
    public async Task<List<MusteriCihazi>> CihazlarAsync(int kurumId, CancellationToken ct = default)
    {
        var liste = await db.Cihazlar.AsNoTracking().Include(c => c.Model)
            .Where(c => c.KurumId == kurumId).OrderBy(c => c.SeriNo).ToListAsync(ct);
        return liste.Select(Donustur).ToList();
    }

    public async Task<MusteriCihazDetayi?> CihazAsync(int kurumId, int cihazId, CancellationToken ct = default)
    {
        var c = await db.Cihazlar.AsNoTracking().Include(x => x.Model)
            .FirstOrDefaultAsync(x => x.Id == cihazId && x.KurumId == kurumId, ct);
        if (c is null)
        {
            return null;
        }

        var talepler = await db.ServisTalepleri.AsNoTracking()
            .Where(t => t.CihazId == c.Id)
            .OrderByDescending(t => t.OlusturmaZamani)
            .Select(t => new { t.No, t.OlusturmaZamani, t.Konu, t.Durum })
            .ToListAsync(ct);
        return new MusteriCihazDetayi(Donustur(c),
            talepler.Select(t => new MusteriTalebi(t.No, t.OlusturmaZamani, TalepKonusuBilgi.Ad(t.Konu), t.Durum, TalepDurumAkisi.Ad(t.Durum))).ToList());
    }

    private static MusteriCihazi Donustur(Cihaz c) => new(
        c.Id, SeriNumarasi.Goster(c.SeriNo), c.Model.Ad, c.KurulumTarihi,
        DogrulamaServisi.GarantiBitisi(c.KurulumTarihi, c.GarantiBitis, c.Model.VarsayilanGarantiAy), c.HizmetSeviyesi.ToString());
}
