using System.Globalization;
using CihazServis.Application.Dogrulama;
using CihazServis.Application.Ortak;

namespace CihazServis.Tests.Birim;

public class SeriNumarasiTestleri
{
    [Theory]
    [InlineData("DMO-A240-0001", "DMOA2400001")]
    [InlineData("dmo-a240-0001", "DMOA2400001")]
    [InlineData("  dmo a240 0001 ", "DMOA2400001")]
    [InlineData("DMO_A240.0001", "DMOA2400001")]
    [InlineData("DMO–A240—0001", "DMOA2400001")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_bicim_farklarini_tek_bicime_indirir(string? girdi, string beklenen)
        => Assert.Equal(beklenen, SeriNumarasi.Normalize(girdi));

    [Fact]
    public void Normalize_Turkce_kulturde_de_ayni_sonucu_verir()
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            // tr-TR'de "i".ToUpper() == "İ" olur; normalizasyon kültürden bağımsız olmalı.
            Assert.Equal("DMOI2400001", SeriNumarasi.Normalize("dmo-i240-0001"));
        }
        finally
        {
            CultureInfo.CurrentCulture = onceki;
        }
    }

    [Fact]
    public void Normalize_asiri_uzun_girdiyi_keser()
        => Assert.True(SeriNumarasi.Normalize(new string('A', 5000)).Length <= SeriNumarasi.AzamiUzunluk);

    [Fact]
    public void Goster_etiket_bicimine_cevirir()
        => Assert.Equal("DMO-A240-0001", SeriNumarasi.Goster("DMOA2400001"));

    [Fact]
    public void Garanti_bitisi_bossa_varsayilan_sureden_hesaplanir()
        => Assert.Equal(new DateOnly(2027, 3, 10), DogrulamaServisi.GarantiBitisi(new DateOnly(2025, 3, 10), null, 24));

    [Fact]
    public void Garanti_bitisi_kayitliysa_o_kullanilir()
        => Assert.Equal(new DateOnly(2029, 10, 1), DogrulamaServisi.GarantiBitisi(new DateOnly(2026, 10, 1), new DateOnly(2029, 10, 1), 24));
}
