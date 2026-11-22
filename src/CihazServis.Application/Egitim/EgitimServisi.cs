using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Egitim;

public enum DersAsamasi
{
    Kilitli,
    VideoAcik,
    QuizAcik,
    Tamamlandi,
}

public sealed record DersDurumu(int DersId, int Sira, string Baslik, string Aciklama, double SureSaniye, DersAsamasi Asama, double IzlenenOran);

public sealed record KursDurumu(int KursId, string Ad, string Aciklama, IReadOnlyList<DersDurumu> Dersler, string? SertifikaNo);

public sealed record OturumBilgisi(
    Guid OturumId, string OynatmaAdresi, string AltyaziAdresi, double SureSaniye, double EnIleriSaniye, double IzlenenOran, bool Tamamlandi);

public sealed record IzlemeBildirimi(Guid OturumId, double Baslangic, double Bitis);

public sealed record IzlemeYaniti(bool Kabul, string? Neden, double EnIleriSaniye, double IzlenenOran, bool Tamamlandi);

/// <summary>
/// Video izlemenin sunucu tarafı. İstemcinin "bitti" demesine güvenilmez: tamamlanma,
/// kabul edilen izleme aralıklarının birleşiminden hesaplanır ve her aralık hem
/// izlenen en ileri noktaya hem de gerçek geçen süreye göre denetlenir.
/// </summary>
public sealed class EgitimServisi(IUygulamaDbContext db, TimeProvider saat, IMedyaImzalayici imzalayici)
{
    public const double TamamlanmaOrani = 0.95;

    /// <summary>Bildirim başlangıcının, izlenen en ileri noktayı aşabileceği en fazla süre.</summary>
    public const double IleriToleransSaniye = 3;

    /// <summary>Oturumda bildirilen toplam izlemenin, gerçek geçen süreyi aşabileceği en fazla süre.</summary>
    public const double SureToleransSaniye = 3;

    public async Task<List<KursDurumu>> KurslarAsync(int kullaniciId, CancellationToken ct = default)
    {
        var kurslar = await db.Kurslar.AsNoTracking().Include(k => k.Dersler).OrderBy(k => k.Id).ToListAsync(ct);
        var ilerlemeler = await db.DersIlerlemeleri.AsNoTracking().Where(i => i.KullaniciId == kullaniciId).ToListAsync(ct);
        var sertifikalar = await db.Sertifikalar.AsNoTracking().Where(s => s.KullaniciId == kullaniciId)
            .Select(s => new { s.KursId, s.No }).ToListAsync(ct);
        return kurslar.Select(k => Hesapla(k, ilerlemeler, sertifikalar.FirstOrDefault(s => s.KursId == k.Id)?.No)).ToList();
    }

    public async Task<(Ders Ders, DersDurumu Durum)?> DersDurumuAsync(int kullaniciId, int dersId, CancellationToken ct = default)
    {
        var ders = await db.Dersler.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dersId, ct);
        if (ders is null)
        {
            return null;
        }

        var kurs = await db.Kurslar.AsNoTracking().Include(k => k.Dersler).FirstAsync(k => k.Id == ders.KursId, ct);
        var dersIdleri = kurs.Dersler.Select(d => d.Id).ToList();
        var ilerlemeler = await db.DersIlerlemeleri.AsNoTracking()
            .Where(i => i.KullaniciId == kullaniciId && dersIdleri.Contains(i.DersId)).ToListAsync(ct);
        return (ders, Hesapla(kurs, ilerlemeler, null).Dersler.First(x => x.DersId == dersId));
    }

    public async Task<Sonuc<OturumBilgisi>> OturumBaslatAsync(int kullaniciId, int dersId, CancellationToken ct = default)
    {
        var durum = await DersDurumuAsync(kullaniciId, dersId, ct);
        if (durum is null)
        {
            return Sonuc<OturumBilgisi>.Bulunamadi("Ders bulunamadı.");
        }

        if (durum.Value.Durum.Asama == DersAsamasi.Kilitli)
        {
            return Sonuc<OturumBilgisi>.Yasak("dersKilitli", "Bu ders, önceki dersin soruları geçilmeden açılmaz.");
        }

        var ders = durum.Value.Ders;
        var i = await IlerlemeAsync(kullaniciId, dersId, ct);
        i.AktifOturum = Guid.NewGuid();
        i.OturumBaslangic = saat.GetUtcNow();
        i.OturumIzlenenSaniye = 0;
        await db.SaveChangesAsync(ct);

        var imza = imzalayici.Imzala(ders.MedyaAnahtari, kullaniciId);
        return Sonuc<OturumBilgisi>.Tamam(new OturumBilgisi(
            i.AktifOturum.Value,
            $"/medya/{ders.MedyaAnahtari}/master.m3u8?{imza}",
            $"/medya/{ders.MedyaAnahtari}/altyazi.vtt?{imza}",
            ders.SureSaniye, i.EnIleriSaniye, Oran(i, ders), i.VideoTamamlandi is not null));
    }

    public async Task<Sonuc<IzlemeYaniti>> IzlemeBildirAsync(int kullaniciId, int dersId, IzlemeBildirimi b, CancellationToken ct = default)
    {
        var i = await db.DersIlerlemeleri.Include(x => x.Ders)
            .FirstOrDefaultAsync(x => x.KullaniciId == kullaniciId && x.DersId == dersId, ct);
        if (i?.AktifOturum is null || i.OturumBaslangic is null)
        {
            return Sonuc<IzlemeYaniti>.Bulunamadi("Bu ders için açık bir oynatma oturumu yok.");
        }

        if (i.AktifOturum != b.OturumId)
        {
            return Sonuc<IzlemeYaniti>.Cakisma("baskaOturum",
                "Bu eğitim başka bir sekmede ya da cihazda açıldı. Bu penceredeki oynatma durduruldu.");
        }

        var sure = i.Ders.SureSaniye;
        if (!double.IsFinite(b.Baslangic) || !double.IsFinite(b.Bitis) || b.Baslangic < 0 || b.Bitis < b.Baslangic || b.Bitis > sure + 1)
        {
            return Red(i, "gecersizAralik", "Bildirilen izleme aralığı geçersiz.");
        }

        if (i.VideoTamamlandi is null)
        {
            if (b.Baslangic > i.EnIleriSaniye + IleriToleransSaniye)
            {
                return Red(i, "ileriAtlama", "Video ileri sarılamaz. İzlediğiniz en ileri noktadan devam edin.");
            }

            var bitis = Math.Min(b.Bitis, sure);
            var parca = bitis - b.Baslangic;
            var gecen = (saat.GetUtcNow() - i.OturumBaslangic.Value).TotalSeconds;
            if (i.OturumIzlenenSaniye + parca > gecen + SureToleransSaniye)
            {
                return Red(i, "hizliIzleme", "Bildirilen izleme süresi gerçekte geçen süreden uzun. Video normal hızda izlenmelidir.");
            }

            var araliklar = IzlemeAraliklari.Ekle(IzlemeAraliklari.Coz(i.IzlenenAraliklar), b.Baslangic, bitis);
            i.OturumIzlenenSaniye += parca;
            i.IzlenenAraliklar = IzlemeAraliklari.Yaz(araliklar);
            i.IzlenenSaniye = IzlemeAraliklari.Toplam(araliklar);
            i.EnIleriSaniye = Math.Max(i.EnIleriSaniye, bitis);
            if (i.IzlenenSaniye >= sure * TamamlanmaOrani)
            {
                i.VideoTamamlandi = saat.GetUtcNow();
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Sonuc<IzlemeYaniti>.Cakisma("tekrarDene", "Aynı anda iki bildirim geldi, sonraki bildirim yeniden denenecek.");
            }
        }

        return Sonuc<IzlemeYaniti>.Tamam(new IzlemeYaniti(true, null, i.EnIleriSaniye, Oran(i, i.Ders), i.VideoTamamlandi is not null));
    }

    internal async Task<DersIlerlemesi> IlerlemeAsync(int kullaniciId, int dersId, CancellationToken ct)
    {
        var i = await db.DersIlerlemeleri.FirstOrDefaultAsync(x => x.KullaniciId == kullaniciId && x.DersId == dersId, ct);
        if (i is null)
        {
            i = new DersIlerlemesi { KullaniciId = kullaniciId, DersId = dersId };
            db.DersIlerlemeleri.Add(i);
        }

        return i;
    }

    private static Sonuc<IzlemeYaniti> Red(DersIlerlemesi i, string kod, string mesaj) =>
        new(new IzlemeYaniti(false, kod, i.EnIleriSaniye, Oran(i, i.Ders), i.VideoTamamlandi is not null), HataTuru.Gecersiz, kod, mesaj);

    private static double Oran(DersIlerlemesi? i, Ders d)
        => i is null || d.SureSaniye <= 0 ? 0 : Math.Min(1, Math.Round(i.IzlenenSaniye / d.SureSaniye, 3));

    private static KursDurumu Hesapla(Kurs k, List<DersIlerlemesi> ilerlemeler, string? sertifikaNo)
    {
        var liste = new List<DersDurumu>();
        var oncekiTamam = true;
        foreach (var d in k.Dersler.OrderBy(d => d.Sira))
        {
            var i = ilerlemeler.FirstOrDefault(x => x.DersId == d.Id);
            var asama = !oncekiTamam ? DersAsamasi.Kilitli
                : i?.QuizGecildi is not null ? DersAsamasi.Tamamlandi
                : i?.VideoTamamlandi is not null ? DersAsamasi.QuizAcik
                : DersAsamasi.VideoAcik;
            liste.Add(new DersDurumu(d.Id, d.Sira, d.Baslik, d.Aciklama, d.SureSaniye, asama, Oran(i, d)));
            oncekiTamam = asama == DersAsamasi.Tamamlandi;
        }

        return new KursDurumu(k.Id, k.Ad, k.Aciklama, liste, sertifikaNo);
    }
}
