using System.Globalization;
using System.Text.RegularExpressions;
using CihazServis.Application.Varliklar;

namespace CihazServis.Application.Servis;

/// <summary>Türkiye saati. 2016'dan beri yaz saati uygulanmadığı için sabit +03:00.</summary>
public static class TurkiyeSaati
{
    public static readonly TimeSpan Fark = TimeSpan.FromHours(3);

    public static DateTimeOffset Yerel(DateTimeOffset utc) => utc.ToOffset(Fark);

    public static DateOnly Bugun(DateTimeOffset utc) => DateOnly.FromDateTime(Yerel(utc).DateTime);
}

/// <summary>Talebin geçebileceği durumlar. Tamamlanan ya da iptal edilen talep yeniden açılmaz.</summary>
public static class TalepDurumAkisi
{
    private static readonly Dictionary<TalepDurumu, TalepDurumu[]> Gecisler = new()
    {
        [TalepDurumu.Alindi] = [TalepDurumu.Inceleniyor, TalepDurumu.Iptal],
        [TalepDurumu.Inceleniyor] = [TalepDurumu.ParcaBekleniyor, TalepDurumu.Serviste, TalepDurumu.Tamamlandi, TalepDurumu.Iptal],
        [TalepDurumu.ParcaBekleniyor] = [TalepDurumu.Serviste, TalepDurumu.Iptal],
        [TalepDurumu.Serviste] = [TalepDurumu.ParcaBekleniyor, TalepDurumu.Tamamlandi, TalepDurumu.Iptal],
        [TalepDurumu.Tamamlandi] = [],
        [TalepDurumu.Iptal] = [],
    };

    /// <summary>Ana akışın sırası, zaman çizelgesinde sıradaki adımı göstermek için.</summary>
    public static readonly TalepDurumu[] AnaAkis =
        [TalepDurumu.Alindi, TalepDurumu.Inceleniyor, TalepDurumu.ParcaBekleniyor, TalepDurumu.Serviste, TalepDurumu.Tamamlandi];

    public static IReadOnlyList<TalepDurumu> Sonrakiler(TalepDurumu d) => Gecisler[d];

    public static bool GecebilirMi(TalepDurumu eski, TalepDurumu yeni) => Gecisler[eski].Contains(yeni);

    public static bool KapaliMi(TalepDurumu d) => d is TalepDurumu.Tamamlandi or TalepDurumu.Iptal;

    public static string Ad(TalepDurumu d) => d switch
    {
        TalepDurumu.Alindi => "Alındı",
        TalepDurumu.Inceleniyor => "İnceleniyor",
        TalepDurumu.ParcaBekleniyor => "Parça bekleniyor",
        TalepDurumu.Serviste => "Serviste",
        TalepDurumu.Tamamlandi => "Tamamlandı",
        TalepDurumu.Iptal => "İptal edildi",
        _ => d.ToString(),
    };

    public static string SiradakiAdim(TalepDurumu d) => d switch
    {
        TalepDurumu.Alindi => "Talebiniz servis ekibine atandı, ilk inceleme yapılacak.",
        TalepDurumu.Inceleniyor => "Ekip arızayı inceliyor, gerekirse parça siparişi ya da servis randevusu verilecek.",
        TalepDurumu.ParcaBekleniyor => "Gereken parça bekleniyor, parça gelince servis işlemine geçilecek.",
        TalepDurumu.Serviste => "Cihaz üzerinde servis işlemi yapılıyor.",
        TalepDurumu.Tamamlandi => "Talep tamamlandı. Sorun devam ederse yeni talep açabilirsiniz.",
        TalepDurumu.Iptal => "Talep iptal edildi.",
        _ => "",
    };
}

public static class TalepKonusuBilgi
{
    public static string Ad(TalepKonusu k) => k switch
    {
        TalepKonusu.Ariza => "Arıza",
        TalepKonusu.Bakim => "Bakım",
        TalepKonusu.Kurulum => "Kurulum",
        _ => "Diğer",
    };

    /// <summary>Konuya göre hedef süre (iş günü). Plus hizmet seviyesinde bir gün kısalır.</summary>
    public static int HedefIsGunu(TalepKonusu k, HizmetSeviyesi? seviye)
    {
        var gun = k switch
        {
            TalepKonusu.Ariza => 3,
            TalepKonusu.Bakim => 5,
            TalepKonusu.Kurulum => 7,
            _ => 5,
        };
        return seviye == HizmetSeviyesi.Plus ? Math.Max(1, gun - 1) : gun;
    }
}

/// <summary>Hafta sonlarını ve sabit tarihli resmî tatilleri atlayan iş günü hesabı.</summary>
public static class IsGunuTakvimi
{
    private static readonly (int Ay, int Gun)[] SabitTatiller =
        [(1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)];

    public static bool IsGunuMu(DateOnly t)
        => t.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
           && !SabitTatiller.Contains((t.Month, t.Day));

    /// <summary>Başlangıç gününü saymadan <paramref name="gun"/> iş günü ileri gider.</summary>
    public static DateOnly Ekle(DateOnly baslangic, int gun)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gun);
        var t = baslangic;
        while (gun > 0)
        {
            t = t.AddDays(1);
            if (IsGunuMu(t))
            {
                gun--;
            }
        }

        return t;
    }

    /// <summary>Başlangıç gününü saymadan <paramref name="gun"/> iş günü geri gider.</summary>
    public static DateOnly Cikar(DateOnly baslangic, int gun)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gun);
        var t = baslangic;
        while (gun > 0)
        {
            t = t.AddDays(-1);
            if (IsGunuMu(t))
            {
                gun--;
            }
        }

        return t;
    }
}

public static class Telefon
{
    /// <summary>"0 (555) 000 12 34", "+90 555 000 1234" gibi girdileri 10 haneye indirir. Geçersizse null.</summary>
    public static string? Normalize(string? girdi)
    {
        var rakam = string.Concat((girdi ?? "").Where(char.IsAsciiDigit));
        if (rakam.Length == 12 && rakam.StartsWith("90", StringComparison.Ordinal))
        {
            rakam = rakam[2..];
        }
        else if (rakam.Length == 11 && rakam[0] == '0')
        {
            rakam = rakam[1..];
        }

        return rakam.Length == 10 && rakam[0] != '0' ? rakam : null;
    }

    public static string? Son4Normalize(string? girdi)
    {
        var rakam = string.Concat((girdi ?? "").Where(c => !char.IsWhiteSpace(c)));
        return rakam.Length == 4 && rakam.All(char.IsAsciiDigit) ? rakam : null;
    }

    public static string Maskele(string telefon) => telefon.Length == 10 ? $"0{telefon[..3]} *** ** {telefon[8..]}" : "***";
}

public static partial class TalepNumarasi
{
    public const int Uzunluk = 14;

    [GeneratedRegex("^SR([0-9]{4})([0-9]{6})$")]
    private static partial Regex Desen();

    public static string Olustur(int yil, int sira) => string.Create(CultureInfo.InvariantCulture, $"SR-{yil:D4}-{sira:D6}");

    /// <summary>"sr 2026 000001", "SR2026000001" gibi yazımları tek biçime indirir. Geçersizse null.</summary>
    public static string? Normalize(string? girdi)
    {
        var temiz = string.Concat((girdi ?? "").Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        var m = Desen().Match(temiz);
        return m.Success ? $"SR-{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
    }
}

public sealed record DosyaTuru(string IcerikTuru, string[] Uzantilar);

/// <summary>
/// Dosya türünü uzantıdan değil ilk baytlardan tespit eder. Uzantı tespit edilen türle
/// uyuşmuyorsa dosya reddedilir: "fatura.pdf" adlı bir çalıştırılabilir dosya kabul edilmez.
/// </summary>
public static class DosyaImzasi
{
    public const int OkunacakBayt = 12;

    public static readonly DosyaTuru Jpeg = new("image/jpeg", [".jpg", ".jpeg"]);
    public static readonly DosyaTuru Png = new("image/png", [".png"]);
    public static readonly DosyaTuru Pdf = new("application/pdf", [".pdf"]);
    public static readonly DosyaTuru Mp4 = new("video/mp4", [".mp4"]);

    public static DosyaTuru? Tespit(ReadOnlySpan<byte> bas)
    {
        if (bas.Length >= 3 && bas[0] == 0xFF && bas[1] == 0xD8 && bas[2] == 0xFF)
        {
            return Jpeg;
        }

        if (bas.Length >= 8 && bas[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        if (bas.Length >= 5 && bas[..5].SequenceEqual("%PDF-"u8))
        {
            return Pdf;
        }

        // ISO tabanlı medya: ilk 4 bayt kutu boyu, ardından "ftyp".
        if (bas.Length >= 12 && bas[4..8].SequenceEqual("ftyp"u8))
        {
            return Mp4;
        }

        return null;
    }

    /// <summary>İmza tanınmışsa ve uzantı o türe aitse türü döner, değilse null.</summary>
    public static DosyaTuru? Dogrula(string dosyaAdi, ReadOnlySpan<byte> bas)
    {
        var tur = Tespit(bas);
        var uzanti = Path.GetExtension(dosyaAdi).ToLowerInvariant();
        return tur is not null && tur.Uzantilar.Contains(uzanti) ? tur : null;
    }
}
