using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;

namespace CihazServis.Tests.Birim;

public sealed class IsGunuTakvimiTestleri
{
    [Theory]
    [InlineData("2026-11-20", 1, "2026-11-23")] // Cuma + 1 = Pazartesi
    [InlineData("2026-11-20", 3, "2026-11-25")]
    [InlineData("2026-11-21", 1, "2026-11-23")] // Cumartesi açılan talep
    [InlineData("2026-11-23", 5, "2026-11-30")]
    [InlineData("2026-10-28", 1, "2026-10-30")] // 29 Ekim atlanır
    [InlineData("2026-12-31", 1, "2027-01-04")] // 1 Ocak Cuma + hafta sonu
    [InlineData("2026-11-20", 0, "2026-11-20")]
    public void Hafta_sonu_ve_sabit_tatiller_atlanir(string bas, int gun, string beklenen)
        => Assert.Equal(DateOnly.Parse(beklenen), IsGunuTakvimi.Ekle(DateOnly.Parse(bas), gun));

    [Fact]
    public void Plus_hizmet_seviyesi_hedefi_bir_gun_kisaltir()
    {
        Assert.Equal(3, TalepKonusuBilgi.HedefIsGunu(TalepKonusu.Ariza, HizmetSeviyesi.Standart));
        Assert.Equal(2, TalepKonusuBilgi.HedefIsGunu(TalepKonusu.Ariza, HizmetSeviyesi.Plus));
        Assert.Equal(3, TalepKonusuBilgi.HedefIsGunu(TalepKonusu.Ariza, null));
    }

    [Fact]
    public void Turkiye_tarihi_utc_gece_yarisindan_once_degisir()
        => Assert.Equal(new DateOnly(2026, 11, 21), TurkiyeSaati.Bugun(new DateTimeOffset(2026, 11, 20, 22, 30, 0, TimeSpan.Zero)));
}

public sealed class TalepDurumAkisiTestleri
{
    [Theory]
    [InlineData(TalepDurumu.Alindi, TalepDurumu.Inceleniyor)]
    [InlineData(TalepDurumu.Inceleniyor, TalepDurumu.ParcaBekleniyor)]
    [InlineData(TalepDurumu.ParcaBekleniyor, TalepDurumu.Serviste)]
    [InlineData(TalepDurumu.Serviste, TalepDurumu.Tamamlandi)]
    [InlineData(TalepDurumu.Alindi, TalepDurumu.Iptal)]
    [InlineData(TalepDurumu.Serviste, TalepDurumu.Iptal)]
    public void Gecerli_gecisler(TalepDurumu eski, TalepDurumu yeni) => Assert.True(TalepDurumAkisi.GecebilirMi(eski, yeni));

    [Theory]
    [InlineData(TalepDurumu.Alindi, TalepDurumu.Tamamlandi)]
    [InlineData(TalepDurumu.Alindi, TalepDurumu.Serviste)]
    [InlineData(TalepDurumu.Tamamlandi, TalepDurumu.Inceleniyor)]
    [InlineData(TalepDurumu.Iptal, TalepDurumu.Alindi)]
    [InlineData(TalepDurumu.Serviste, TalepDurumu.Alindi)]
    public void Gecersiz_gecisler(TalepDurumu eski, TalepDurumu yeni) => Assert.False(TalepDurumAkisi.GecebilirMi(eski, yeni));

    [Fact]
    public void Kapali_talepten_hicbir_yere_gecilmez()
    {
        Assert.Empty(TalepDurumAkisi.Sonrakiler(TalepDurumu.Tamamlandi));
        Assert.Empty(TalepDurumAkisi.Sonrakiler(TalepDurumu.Iptal));
    }

    [Fact]
    public void Her_acik_durumdan_iptal_edilebilir()
    {
        foreach (var d in Enum.GetValues<TalepDurumu>().Where(d => !TalepDurumAkisi.KapaliMi(d)))
        {
            Assert.Contains(TalepDurumu.Iptal, TalepDurumAkisi.Sonrakiler(d));
        }
    }
}

public sealed class TalepBicimTestleri
{
    [Theory]
    [InlineData("0555 123 45 67", "5551234567")]
    [InlineData("+90 (555) 123-4567", "5551234567")]
    [InlineData("5551234567", "5551234567")]
    [InlineData("0216 555 00 00", "2165550000")]
    [InlineData("12345", null)]
    [InlineData("0055512345", null)]
    [InlineData("", null)]
    public void Telefon_normalize(string girdi, string? beklenen) => Assert.Equal(beklenen, Telefon.Normalize(girdi));

    [Theory]
    [InlineData("SR-2026-000001", "SR-2026-000001")]
    [InlineData("sr 2026 000001", "SR-2026-000001")]
    [InlineData("SR2026000001", "SR-2026-000001")]
    [InlineData(" sr-2026-000001 ", "SR-2026-000001")]
    [InlineData("SR-2026-01", null)]
    [InlineData("XX-2026-000001", null)]
    [InlineData("SR-2026-0000012", null)]
    public void Talep_no_normalize(string girdi, string? beklenen) => Assert.Equal(beklenen, TalepNumarasi.Normalize(girdi));

    [Fact]
    public void Talep_no_olustur() => Assert.Equal("SR-2026-000042", TalepNumarasi.Olustur(2026, 42));

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\x\\foto.jpg", "foto.jpg")]
    [InlineData("a\"<b>.png", "ab.png")]
    public void Dosya_adi_yoldan_arindirilir(string girdi, string beklenen) => Assert.Equal(beklenen, ServisTalebiServisi.GuvenliAd(girdi));
}

public sealed class DosyaImzasiTestleri
{
    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    public static readonly byte[] Pdf = "%PDF-1.7\n%âã"u8.ToArray();
    public static readonly byte[] Mp4 = [0, 0, 0, 0x20, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D];
    public static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0];

    [Theory]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("FOTO.JPEG", "image/jpeg")]
    public void Jpeg_tanınır(string ad, string tur) => Assert.Equal(tur, DosyaImzasi.Dogrula(ad, Jpeg)?.IcerikTuru);

    [Fact]
    public void Dort_tur_imzadan_tanınır()
    {
        Assert.Equal("image/png", DosyaImzasi.Dogrula("a.png", Png)?.IcerikTuru);
        Assert.Equal("application/pdf", DosyaImzasi.Dogrula("a.pdf", Pdf)?.IcerikTuru);
        Assert.Equal("video/mp4", DosyaImzasi.Dogrula("a.mp4", Mp4)?.IcerikTuru);
    }

    [Fact]
    public void Uzantisi_pdf_olan_calistirilabilir_dosya_reddedilir() => Assert.Null(DosyaImzasi.Dogrula("fatura.pdf", Exe));

    [Fact]
    public void Gercek_png_ama_uzantisi_jpg_reddedilir() => Assert.Null(DosyaImzasi.Dogrula("foto.jpg", Png));

    [Fact]
    public void Kisa_ya_da_bos_icerik_reddedilir()
    {
        Assert.Null(DosyaImzasi.Dogrula("a.jpg", []));
        Assert.Null(DosyaImzasi.Dogrula("a.mp4", Mp4[..6]));
    }
}
