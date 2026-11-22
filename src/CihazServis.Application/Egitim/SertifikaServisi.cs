using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CihazServis.Application.Ortak;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Egitim;

/// <summary>Herkese açık doğrulama sonucu: tam ad yok, yalnız baş harfler.</summary>
public sealed record SertifikaDogrulamaDto(
    string No, string BasHarfler, string KursAdi, string? ModelAdi, DateTimeOffset VerilisZamani, bool Gecerli, DateTimeOffset? IptalZamani);

public sealed record SertifikaPdfVerisi(string No, string AdSoyad, string KursAdi, string? ModelAdi, DateTimeOffset VerilisZamani);

public sealed record SertifikaOzeti(string No, string KursAdi, DateTimeOffset VerilisZamani, bool Gecerli);

public interface ISertifikaPdfUretici
{
    byte[] Uret(SertifikaPdfVerisi veri, string dogrulamaAdresi);
}

public sealed class SertifikaServisi(IUygulamaDbContext db, TimeProvider saat)
{
    public const int NoUzunlugu = 26;
    private const string Base32Alfabe = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private static readonly CultureInfo Tr = new("tr-TR");

    /// <summary>128 bit kriptografik rastgele değer, base32 (26 karakter). Sıra numarası değildir.</summary>
    public static string YeniNo()
    {
        Span<byte> b = stackalloc byte[16];
        RandomNumberGenerator.Fill(b);
        var sb = new StringBuilder(NoUzunlugu);
        int tampon = 0, bit = 0;
        foreach (var x in b)
        {
            tampon = (tampon << 8) | x;
            bit += 8;
            while (bit >= 5)
            {
                sb.Append(Base32Alfabe[(tampon >> (bit - 5)) & 31]);
                bit -= 5;
            }
        }

        if (bit > 0)
        {
            sb.Append(Base32Alfabe[(tampon << (5 - bit)) & 31]);
        }

        return sb.ToString();
    }

    public static string? NoNormalize(string? girdi)
    {
        var n = string.Concat((girdi ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        return n.Length == NoUzunlugu && n.All(c => Base32Alfabe.Contains(c, StringComparison.Ordinal)) ? n : null;
    }

    public static string BasHarfler(string adSoyad)
        => string.Join(" ", adSoyad.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpper(p[0], Tr) + "."));

    /// <summary>
    /// Kursun son quiz'i geçildiğinde çağrılır. Geçilen ders henüz kaydedilmemiş olabilir,
    /// bu yüzden yalnız diğer derslerin durumuna bakılır. Sertifika aynı SaveChanges ile yazılır.
    /// </summary>
    public async Task<string?> KursBittiyseVerAsync(int kullaniciId, int gecilenDersId, CancellationToken ct = default)
    {
        var kursId = await db.Dersler.Where(d => d.Id == gecilenDersId).Select(d => d.KursId).FirstAsync(ct);
        var digerDersler = await db.Dersler.Where(d => d.KursId == kursId && d.Id != gecilenDersId).Select(d => d.Id).ToListAsync(ct);
        var gecilenler = await db.DersIlerlemeleri
            .CountAsync(i => i.KullaniciId == kullaniciId && digerDersler.Contains(i.DersId) && i.QuizGecildi != null, ct);
        if (gecilenler < digerDersler.Count)
        {
            return null;
        }

        var mevcut = await db.Sertifikalar.Where(s => s.KullaniciId == kullaniciId && s.KursId == kursId).Select(s => s.No).FirstOrDefaultAsync(ct);
        if (mevcut is not null)
        {
            return mevcut;
        }

        var kullanici = await db.Kullanicilar.FirstAsync(k => k.Id == kullaniciId, ct);
        var simdi = saat.GetUtcNow();
        var s = new Sertifika { No = YeniNo(), KullaniciId = kullaniciId, KursId = kursId, AdSoyad = kullanici.AdSoyad, VerilisZamani = simdi };
        s.Hareketler.Add(new SertifikaHareketi { Tur = SertifikaHareketTuru.Verildi, Zaman = simdi });
        db.Sertifikalar.Add(s);
        return s.No;
    }

    public async Task<SertifikaDogrulamaDto?> DogrulaAsync(string? no, CancellationToken ct = default)
    {
        var n = NoNormalize(no);
        if (n is null)
        {
            return null;
        }

        var s = await db.Sertifikalar.AsNoTracking()
            .Include(x => x.Kurs).ThenInclude(k => k.Model)
            .Include(x => x.Hareketler)
            .FirstOrDefaultAsync(x => x.No == n, ct);
        if (s is null)
        {
            return null;
        }

        var son = s.Hareketler.OrderBy(h => h.Zaman).ThenBy(h => h.Id).Last();
        return new SertifikaDogrulamaDto(s.No, BasHarfler(s.AdSoyad), s.Kurs.Ad, s.Kurs.Model?.Ad, s.VerilisZamani,
            son.Tur == SertifikaHareketTuru.Verildi, son.Tur == SertifikaHareketTuru.IptalEdildi ? son.Zaman : null);
    }

    public async Task<List<SertifikaOzeti>> KullaniciSertifikalariAsync(int kullaniciId, CancellationToken ct = default)
    {
        var liste = await db.Sertifikalar.AsNoTracking().Include(s => s.Kurs).Include(s => s.Hareketler)
            .Where(s => s.KullaniciId == kullaniciId).ToListAsync(ct);
        return liste.Select(s => new SertifikaOzeti(s.No, s.Kurs.Ad, s.VerilisZamani,
            s.Hareketler.OrderBy(h => h.Zaman).ThenBy(h => h.Id).Last().Tur == SertifikaHareketTuru.Verildi)).ToList();
    }

    /// <summary>PDF yalnız sertifikanın sahibine verilir; başkasının numarası "yok" görünür.</summary>
    public async Task<SertifikaPdfVerisi?> PdfVerisiAsync(int kullaniciId, string? no, CancellationToken ct = default)
    {
        var n = NoNormalize(no);
        return n is null ? null : await db.Sertifikalar.AsNoTracking()
            .Where(s => s.No == n && s.KullaniciId == kullaniciId)
            .Select(s => new SertifikaPdfVerisi(s.No, s.AdSoyad, s.Kurs.Ad, s.Kurs.Model != null ? s.Kurs.Model.Ad : null, s.VerilisZamani))
            .FirstOrDefaultAsync(ct);
    }

    public sealed record SertifikaSatiri(string No, string AdSoyad, string KursAdi, DateTimeOffset VerilisZamani, bool Gecerli);

    /// <summary>Yönetim listesi: en yeni sertifika en üstte.</summary>
    public async Task<List<SertifikaSatiri>> ListeleAsync(CancellationToken ct = default)
    {
        var liste = await db.Sertifikalar.AsNoTracking().Include(s => s.Kurs).Include(s => s.Hareketler)
            .OrderByDescending(s => s.VerilisZamani).Take(200).ToListAsync(ct);
        return liste.Select(s => new SertifikaSatiri(s.No, s.AdSoyad, s.Kurs.Ad, s.VerilisZamani,
            s.Hareketler.OrderBy(h => h.Zaman).ThenBy(h => h.Id).Last().Tur == SertifikaHareketTuru.Verildi)).ToList();
    }

    /// <summary>İptal, silme değil yeni bir harekettir; geçmiş korunur.</summary>
    public async Task<Sonuc<bool>> IptalEtAsync(string no, int yapanKullaniciId, string aciklama, CancellationToken ct = default)
    {
        var n = NoNormalize(no);
        var s = n is null ? null : await db.Sertifikalar.Include(x => x.Hareketler).FirstOrDefaultAsync(x => x.No == n, ct);
        if (s is null)
        {
            return Sonuc<bool>.Bulunamadi("Sertifika bulunamadı.");
        }

        if (s.Hareketler.OrderBy(h => h.Zaman).ThenBy(h => h.Id).Last().Tur == SertifikaHareketTuru.IptalEdildi)
        {
            return Sonuc<bool>.Cakisma("zatenIptal", "Sertifika zaten iptal edilmiş.");
        }

        db.SertifikaHareketleri.Add(new SertifikaHareketi
        {
            SertifikaId = s.Id,
            Tur = SertifikaHareketTuru.IptalEdildi,
            Zaman = saat.GetUtcNow(),
            Aciklama = aciklama,
            YapanKullaniciId = yapanKullaniciId,
        });
        db.Denetle(saat.GetUtcNow(), yapanKullaniciId, "SertifikaIptal", "Sertifika", s.No, aciklama);
        await db.SaveChangesAsync(ct);
        return Sonuc<bool>.Tamam(true);
    }
}
