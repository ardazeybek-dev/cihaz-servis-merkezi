using System.Net;
using CihazServis.Application.Ortak;
using CihazServis.Tests.Altyapi;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class MedyaTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "csm-medya-" + Guid.NewGuid().ToString("N"));
    private UygulamaFabrikasi _f = null!;
    private HttpClient _c = null!;
    private int _kullanici;

    public async Task InitializeAsync()
    {
        var ders = Path.Combine(_klasor, "ders-1");
        Directory.CreateDirectory(Path.Combine(ders, "360p"));
        await File.WriteAllTextAsync(Path.Combine(ders, "master.m3u8"),
            "#EXTM3U\n#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"a\",URI=\"altyazi.m3u8\"\n#EXT-X-STREAM-INF:BANDWIDTH=1\n360p/index.m3u8\n");
        await File.WriteAllTextAsync(Path.Combine(ders, "360p", "index.m3u8"), "#EXTM3U\n#EXTINF:4.0,\nseg_000.ts\n#EXT-X-ENDLIST\n");
        await File.WriteAllBytesAsync(Path.Combine(ders, "360p", "seg_000.ts"), new byte[188]);
        await File.WriteAllTextAsync(Path.Combine(_klasor, "gizli.ts"), "kok disi");

        _f = new UygulamaFabrikasi(sql, new Dictionary<string, string?> { ["Medya:Klasor"] = _klasor });
        _c = _f.Istemci();
        await Yardimci.GirisYap(_c, "musteri@ornek-klinik.test");
        _kullanici = await _f.VeriAsync(db => db.Kullanicilar.Where(k => k.Eposta == "musteri@ornek-klinik.test").Select(k => k.Id).FirstAsync());
    }

    public async Task DisposeAsync()
    {
        await _f.DisposeAsync();
        Directory.Delete(_klasor, recursive: true);
    }

    private string Imza(int kullanici) => _f.Services.GetRequiredService<IMedyaImzalayici>().Imzala("ders-1", kullanici);

    [Fact]
    public async Task Imzasiz_istek_reddedilir()
        => Assert.Equal(HttpStatusCode.Forbidden, (await _c.GetAsync("/medya/ders-1/master.m3u8")).StatusCode);

    [Fact]
    public async Task Gecerli_imzayla_playlist_doner_ve_tum_adresler_imzalanir()
    {
        var imza = Imza(_kullanici);
        var yanit = await _c.GetAsync($"/medya/ders-1/master.m3u8?{imza}");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store, private", string.Join(", ", yanit.Headers.CacheControl!.ToString().Split(", ").Order()));
        var icerik = await yanit.Content.ReadAsStringAsync();
        Assert.Contains($"360p/index.m3u8?{imza}", icerik, StringComparison.Ordinal);
        Assert.Contains($"URI=\"altyazi.m3u8?{imza}\"", icerik, StringComparison.Ordinal);

        var alt = await _c.GetStringAsync($"/medya/ders-1/360p/index.m3u8?{imza}");
        Assert.Contains($"seg_000.ts?{imza}", alt, StringComparison.Ordinal);

        var segment = await _c.GetAsync($"/medya/ders-1/360p/seg_000.ts?{imza}");
        Assert.Equal(HttpStatusCode.OK, segment.StatusCode);
        Assert.Equal("video/mp2t", segment.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Baska_kullanicinin_imzasi_ise_yaramaz()
        => Assert.Equal(HttpStatusCode.Forbidden, (await _c.GetAsync($"/medya/ders-1/master.m3u8?{Imza(_kullanici + 1)}")).StatusCode);

    [Fact]
    public async Task Suresi_dolan_imza_reddedilir()
    {
        var imza = Imza(_kullanici);
        _f.Saat.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.Forbidden, (await _c.GetAsync($"/medya/ders-1/master.m3u8?{imza}")).StatusCode);
    }

    [Fact]
    public async Task Degistirilmis_imza_reddedilir()
    {
        var imza = Imza(_kullanici);
        var bozuk = imza[..^2] + (imza[^1] == 'A' ? "BB" : "AA");
        Assert.Equal(HttpStatusCode.Forbidden, (await _c.GetAsync($"/medya/ders-1/master.m3u8?{bozuk}")).StatusCode);
    }

    [Theory]
    [InlineData("../gizli.ts")]
    [InlineData("..%2Fgizli.ts")]
    [InlineData("360p/../../gizli.ts")]
    public async Task Medya_klasorunun_disina_cikilamaz(string yol)
    {
        var yanit = await _c.GetAsync($"/medya/ders-1/{yol}?{Imza(_kullanici)}");
        Assert.NotEqual(HttpStatusCode.OK, yanit.StatusCode);
    }

    [Fact]
    public async Task Giris_yapmadan_medya_acilmaz()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _f.Istemci().GetAsync($"/medya/ders-1/master.m3u8?{Imza(_kullanici)}")).StatusCode);
}
