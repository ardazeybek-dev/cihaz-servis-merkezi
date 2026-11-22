using System.Text.RegularExpressions;
using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Dogrulama;

public enum DogrulamaSonucu
{
    Bulundu,
    Bulunamadi,
    GecersizBicim,
}

public enum GarantiDurumu
{
    DevamEdiyor,
    YakindaBitiyor,
    Bitti,
}

/// <summary>
/// Herkese açık sorgu yanıtı. Bilerek sahip, kurum, adres ya da kurulum yeri içermez:
/// seri numarasını bilen herkes yalnız cihazın modelini ve garanti durumunu görür.
/// </summary>
public sealed record DogrulamaYaniti(
    DogrulamaSonucu Sonuc,
    string? SeriNo,
    ModelOzeti? Model,
    GarantiOzeti? Garanti,
    string? HizmetSeviyesi,
    string Mesaj,
    string ServisTalebiAdresi);

public sealed record ModelOzeti(string Kod, string Ad);

public sealed record GarantiOzeti(GarantiDurumu Durum, DateOnly Bitis, int KalanGun);

public sealed class DogrulamaServisi(IUygulamaDbContext db, TimeProvider saat, IIpOzetleyici ipOzetleyici)
{
    public const int YakindaBitiyorGun = 30;

    public async Task<DogrulamaYaniti> SorgulaAsync(string? seriNoGirdisi, string? tuzakAlan, string? ip, CancellationToken ct = default)
    {
        var seri = SeriNumarasi.Normalize(seriNoGirdisi);
        var tuzakDolu = !string.IsNullOrEmpty(tuzakAlan);
        DogrulamaYaniti yanit;

        if (tuzakDolu)
        {
            // Bot olduğu belli: gerçek sonucu vermeden "bulunamadı" görünümü döner.
            yanit = Bulunamadi(seri);
        }
        else
        {
            yanit = await HesaplaAsync(seri, ct);
        }

        db.DogrulamaSorgulari.Add(new DogrulamaSorgusu
        {
            Zaman = saat.GetUtcNow(),
            IpOzeti = ipOzetleyici.Ozetle(ip),
            SeriNo = seri.Length > 0 ? seri : null,
            Sonuc = yanit.Sonuc.ToString(),
            TuzakDolu = tuzakDolu,
        });
        await db.SaveChangesAsync(ct);
        return yanit;
    }

    private async Task<DogrulamaYaniti> HesaplaAsync(string seri, CancellationToken ct)
    {
        var modeller = await db.CihazModelleri.AsNoTracking().ToListAsync(ct);
        var model = modeller.FirstOrDefault(m => Regex.IsMatch(seri, m.SeriNoDeseni, RegexOptions.None, TimeSpan.FromMilliseconds(50)));
        if (model is null)
        {
            return new DogrulamaYaniti(DogrulamaSonucu.GecersizBicim, null, null, null, null,
                "Seri numarası tanınan bir biçimde değil. Cihazın arka yüzündeki etikette DMO-A240-0001 biçiminde yazar.",
                ServisAdresi(null));
        }

        var cihaz = await db.Cihazlar.AsNoTracking()
            .Where(c => c.SeriNo == seri)
            .Select(c => new { c.KurulumTarihi, c.GarantiBitis, c.HizmetSeviyesi, Model = c.Model })
            .FirstOrDefaultAsync(ct);
        if (cihaz is null)
        {
            return Bulunamadi(seri);
        }

        var bugun = DateOnly.FromDateTime(saat.GetUtcNow().UtcDateTime);
        var bitis = GarantiBitisi(cihaz.KurulumTarihi, cihaz.GarantiBitis, cihaz.Model.VarsayilanGarantiAy);
        var kalan = bitis.DayNumber - bugun.DayNumber;
        var durum = kalan < 0 ? GarantiDurumu.Bitti : kalan <= YakindaBitiyorGun ? GarantiDurumu.YakindaBitiyor : GarantiDurumu.DevamEdiyor;
        var mesaj = durum switch
        {
            GarantiDurumu.Bitti => "Cihaz kayıtlı, garanti süresi dolmuş. Ücretli servis için talep açabilirsiniz.",
            GarantiDurumu.YakindaBitiyor => $"Cihaz kayıtlı, garanti {kalan} gün içinde bitiyor.",
            _ => "Cihaz kayıtlı, garanti devam ediyor.",
        };

        return new DogrulamaYaniti(DogrulamaSonucu.Bulundu, SeriNumarasi.Goster(seri),
            new ModelOzeti(cihaz.Model.Kod, cihaz.Model.Ad),
            new GarantiOzeti(durum, bitis, Math.Max(kalan, 0)),
            cihaz.HizmetSeviyesi.ToString(), mesaj, ServisAdresi(seri));
    }

    public static DateOnly GarantiBitisi(DateOnly kurulum, DateOnly? kayitliBitis, int varsayilanAy)
        => kayitliBitis ?? kurulum.AddMonths(varsayilanAy);

    private static DogrulamaYaniti Bulunamadi(string seri) => new(
        DogrulamaSonucu.Bulunamadi, seri.Length > 0 ? SeriNumarasi.Goster(seri) : null, null, null, null,
        "Bu seri numarasıyla kayıtlı bir cihaz bulamadık. Numarayı etiketten bir kez daha kontrol edin, yine bulunamazsa servis talebi açın, kaydı biz tamamlarız.",
        ServisAdresi(seri.Length > 0 ? seri : null));

    private static string ServisAdresi(string? seri)
        => seri is null ? "/servis/talep" : $"/servis/talep?seriNo={Uri.EscapeDataString(SeriNumarasi.Goster(seri))}";
}
