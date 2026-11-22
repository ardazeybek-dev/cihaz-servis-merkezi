using Microsoft.Playwright;

namespace CihazServis.E2E;

[Collection(UygulamaKoleksiyonu.Ad)]
public sealed class OynaticiTestleri(UygulamaSureci app)
{
    private static async Task<IPage> OynatAsync(UygulamaSureci app, string kullanici)
    {
        var sayfa = await app.SayfaAsync(kullanici);
        await sayfa.GotoAsync("/egitim");
        await sayfa.ClickAsync("[data-asama=VideoAcik] a");
        await sayfa.WaitForSelectorAsync("[data-oynatici][data-hazir='1']");
        await sayfa.WaitForFunctionAsync("() => document.querySelector('video').readyState >= 2", null, new() { Timeout = 20000 });
        await sayfa.EvaluateAsync("async () => { const v = document.querySelector('video'); v.muted = true; await v.play(); }");
        await sayfa.WaitForFunctionAsync("() => document.querySelector('video').currentTime > 2", null, new() { Timeout = 15000 });
        return sayfa;
    }

    [Fact]
    public async Task Ileri_sarma_denemesi_izlenen_en_ileri_noktaya_doner()
    {
        var sayfa = await OynatAsync(app, "musteri@ornek-klinik.test");
        await sayfa.EvaluateAsync("() => { document.querySelector('video').currentTime = 30; }");
        await sayfa.WaitForTimeoutAsync(800);

        var konum = await sayfa.EvaluateAsync<double>("() => document.querySelector('video').currentTime");
        Assert.InRange(konum, 0, 8);
        var mesaj = await sayfa.Locator("[data-mesaj]").InnerTextAsync();
        Assert.Contains("ileri sarılamaz", mesaj, StringComparison.Ordinal);
        await sayfa.Context.CloseAsync();
    }

    [Fact]
    public async Task Oynatma_hizi_degistirilemez()
    {
        var sayfa = await OynatAsync(app, "musteri@ornek-spor.test");
        await sayfa.EvaluateAsync("() => { document.querySelector('video').playbackRate = 2; }");
        await sayfa.WaitForTimeoutAsync(300);
        Assert.Equal(1, await sayfa.EvaluateAsync<double>("() => document.querySelector('video').playbackRate"));
        await sayfa.Context.CloseAsync();
    }

    [Fact]
    public async Task Geri_sarma_serbesttir_ve_video_HLS_ile_oynar()
    {
        var sayfa = await OynatAsync(app, "personel@servis.test");
        await sayfa.WaitForFunctionAsync("() => document.querySelector('video').currentTime > 4", null, new() { Timeout = 15000 });
        await sayfa.EvaluateAsync("() => { document.querySelector('video').currentTime = 1; }");
        await sayfa.WaitForTimeoutAsync(500);
        var konum = await sayfa.EvaluateAsync<double>("() => document.querySelector('video').currentTime");
        Assert.InRange(konum, 0.5, 3);

        // Video gerçekten imzalı HLS üzerinden geliyor: segmentler /medya altından imzalı adresle istendi.
        var kaynaklar = await sayfa.EvaluateAsync<string[]>(
            "() => performance.getEntriesByType('resource').map(e => e.name).filter(n => n.includes('/medya/'))");
        Assert.Contains(kaynaklar, k => k.Contains(".ts?u=", StringComparison.Ordinal) && k.Contains("&s=", StringComparison.Ordinal));
        await sayfa.Context.CloseAsync();
    }

    [Fact]
    public async Task Ikinci_sekme_acilinca_ilk_sekmedeki_oynatma_durur()
    {
        var ilk = await OynatAsync(app, "yonetici@servis.test");
        var ikinci = await ilk.Context.NewPageAsync();
        await ikinci.GotoAsync(ilk.Url);
        await ikinci.WaitForSelectorAsync("[data-oynatici][data-hazir='1']");

        // İlk sekme sonraki bildiriminde reddedilir: duraklatma bildirimi hemen gönderir.
        await ilk.EvaluateAsync("() => document.querySelector('video').pause()");
        await ilk.WaitForSelectorAsync("[data-mesaj-tur=uyari]", new() { Timeout = 10000 });
        Assert.Contains("başka bir sekmede", await ilk.Locator("[data-mesaj]").InnerTextAsync(), StringComparison.Ordinal);
        await ilk.Context.CloseAsync();
    }
}
