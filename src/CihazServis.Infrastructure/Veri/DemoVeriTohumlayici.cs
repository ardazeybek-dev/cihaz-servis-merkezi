using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Infrastructure.Veri;

/// <summary>
/// Yalnız demo ve geliştirme ortamı için uydurma örnek veri. Gerçek bir firmaya,
/// kişiye ya da cihaza ait değildir.
/// </summary>
public sealed class DemoVeriTohumlayici(UygulamaDbContext db, IPasswordHasher<Kullanici> hasher)
{
    public const string DemoSifre = "Demo.2026!";

    public async Task TohumlaAsync(CancellationToken ct = default)
    {
        if (await db.Kurumlar.AnyAsync(ct))
        {
            return;
        }

        var klinik = new Kurum { Ad = "Örnek Klinik Kadıköy" };
        var spor = new Kurum { Ad = "Örnek Spor Salonu Bornova" };
        db.Kurumlar.AddRange(klinik, spor);

        var a100 = new CihazModeli { Kod = "A100", Ad = "Demo Cihaz A100", SeriNoDeseni = "^DMOA[0-9]{7}$", VarsayilanGarantiAy = 24 };
        var b200 = new CihazModeli { Kod = "B200", Ad = "Demo Cihaz B200", SeriNoDeseni = "^DMOB[0-9]{7}$", VarsayilanGarantiAy = 24 };
        var c300 = new CihazModeli { Kod = "C300", Ad = "Demo Cihaz C300", SeriNoDeseni = "^DMOC[0-9]{7}$", VarsayilanGarantiAy = 12 };
        db.CihazModelleri.AddRange(a100, b200, c300);

        db.Cihazlar.AddRange(
            Cihaz("DMO-A240-0001", a100, klinik, new DateOnly(2025, 3, 10), null, HizmetSeviyesi.Plus),
            Cihaz("DMO-A230-0002", a100, klinik, new DateOnly(2023, 1, 15), new DateOnly(2025, 1, 15), HizmetSeviyesi.Standart),
            Cihaz("DMO-B250-0003", b200, spor, new DateOnly(2026, 10, 1), new DateOnly(2029, 10, 1), HizmetSeviyesi.Plus),
            Cihaz("DMO-C260-0004", c300, spor, new DateOnly(2026, 11, 2), null, HizmetSeviyesi.Standart));

        db.Kullanicilar.AddRange(
            Kullanici("musteri@ornek-klinik.test", "Ayşe Yılmaz", KullaniciRolu.Musteri, klinik),
            Kullanici("musteri@ornek-spor.test", "Mehmet Demir", KullaniciRolu.Musteri, spor),
            Kullanici("personel@servis.test", "Selin Kaya", KullaniciRolu.Personel, null),
            Kullanici("yonetici@servis.test", "Can Öztürk", KullaniciRolu.Yonetici, null));

        await db.SaveChangesAsync(ct);
    }

    private static Cihaz Cihaz(string seri, CihazModeli model, Kurum kurum, DateOnly kurulum, DateOnly? bitis, HizmetSeviyesi seviye) => new()
    {
        SeriNo = SeriNumarasi.Normalize(seri),
        Model = model,
        Kurum = kurum,
        KurulumTarihi = kurulum,
        GarantiBitis = bitis,
        HizmetSeviyesi = seviye,
    };

    private Kullanici Kullanici(string eposta, string ad, KullaniciRolu rol, Kurum? kurum)
    {
        var k = new Kullanici { Eposta = eposta, AdSoyad = ad, Rol = rol, Kurum = kurum };
        k.SifreOzeti = hasher.HashPassword(k, DemoSifre);
        return k;
    }
}
