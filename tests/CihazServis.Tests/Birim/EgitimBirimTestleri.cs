using CihazServis.Application.Egitim;
using CihazServis.Web.Api;

namespace CihazServis.Tests.Birim;

public class EgitimBirimTestleri
{
    [Fact]
    public void Cakisan_ve_bitisik_araliklar_birlesir()
    {
        var a = IzlemeAraliklari.Ekle([], 0, 10);
        a = IzlemeAraliklari.Ekle(a, 20, 30);
        a = IzlemeAraliklari.Ekle(a, 10.3, 20);
        Assert.Equal([(0d, 30d)], a);
        Assert.Equal(30, IzlemeAraliklari.Toplam(a));
    }

    [Fact]
    public void Ayni_kismi_tekrar_izlemek_toplami_artirmaz()
    {
        var a = IzlemeAraliklari.Ekle([(0, 20)], 5, 15);
        Assert.Equal(20, IzlemeAraliklari.Toplam(a));
    }

    [Fact]
    public void Araliklar_JSON_a_yazilip_okunabilir()
    {
        var a = IzlemeAraliklari.Ekle(IzlemeAraliklari.Ekle([], 0, 9.999), 15, 20);
        Assert.Equal(a.Select(x => (Math.Round(x.Bas, 2), Math.Round(x.Son, 2))), IzlemeAraliklari.Coz(IzlemeAraliklari.Yaz(a)));
    }

    [Fact]
    public void Sertifika_numaralari_benzersiz_ve_base32()
    {
        var numaralar = Enumerable.Range(0, 2000).Select(_ => SertifikaServisi.YeniNo()).ToList();
        Assert.Equal(numaralar.Count, numaralar.Distinct().Count());
        Assert.All(numaralar, n => Assert.Matches("^[A-Z2-7]{26}$", n));
    }

    [Theory]
    [InlineData("Ayşe Yılmaz", "A. Y.")]
    [InlineData("ismail ışık", "İ. I.")]
    [InlineData("Can  Öztürk", "C. Ö.")]
    public void Bas_harfler_Turkce_kurallarla_uretilir(string ad, string beklenen)
        => Assert.Equal(beklenen, SertifikaServisi.BasHarfler(ad));

    [Theory]
    [InlineData("abcd efgh-ijkl mnop qrst uvwx yz", "ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [InlineData("KISA", null)]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY1", null)]
    public void Sertifika_numarasi_normalize_edilir(string girdi, string? beklenen)
        => Assert.Equal(beklenen, SertifikaServisi.NoNormalize(girdi));

    [Fact]
    public void Playlist_yeniden_yazilirken_yorumlar_korunur_adresler_imzalanir()
    {
        var sonuc = ApiUclari.PlaylistYenidenYaz("#EXTM3U\r\n#EXTINF:4.0,\r\nseg_000.ts\r\n\r\nhttps://cdn.example/x.ts\n", "u=1&e=2&s=3");
        Assert.Equal("#EXTM3U\n#EXTINF:4.0,\nseg_000.ts?u=1&e=2&s=3\n\nhttps://cdn.example/x.ts\n", sonuc);
    }
}
