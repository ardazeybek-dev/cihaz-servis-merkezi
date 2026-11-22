using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace CihazServis.E2E;

/// <summary>
/// Her sayfa WCAG 2.1 AA otomatik denetiminden (axe-core) geçer ve 320 px genişlikte yatay kaydırma oluşturmaz.
/// </summary>
[Collection(UygulamaKoleksiyonu.Ad)]
public sealed class KaliteTestleri(UygulamaSureci app)
{
    public static TheoryData<string?, string> Sayfalar => new()
    {
        { null, "/" },
        { null, "/garanti" },
        { null, "/giris" },
        { null, "/aydinlatma-metni" },
        { null, "/servis/talep" },
        { null, "/servis/durum" },
        { null, "/sertifika" },
        { null, "/durum/404" },
        { "musteri@ornek-klinik.test", "/egitim" },
        { "musteri@ornek-klinik.test", "/egitim/ders/1" },
        { "musteri@ornek-klinik.test", "/musteri/cihazlar" },
        { "personel@servis.test", "/yonetim" },
        { "personel@servis.test", "/yonetim/talep/SR-2026-000001" },
        { "personel@servis.test", "/yonetim/sertifikalar" },
        { "yonetici@servis.test", "/yonetim/denetim" },
    };

    private static readonly AxeRunOptions Wcag = new()
    {
        RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
    };

    [Theory]
    [MemberData(nameof(Sayfalar))]
    public async Task Sayfa_erisilebilirlik_denetiminden_gecer(string? giris, string yol)
    {
        var sayfa = await app.SayfaAsync(giris);
        try
        {
            await sayfa.GotoAsync(yol);
            Assert.EndsWith(yol, new Uri(sayfa.Url).AbsolutePath, StringComparison.Ordinal);
            var sonuc = await sayfa.RunAxe(Wcag);
            var ihlaller = sonuc.Violations
                .Select(v => $"{v.Id} ({v.Impact}): {v.Help} → {string.Join(", ", v.Nodes.Select(n => n.Target?.ToString()))}")
                .ToList();
            Assert.True(ihlaller.Count == 0, $"{yol} erişilebilirlik ihlalleri:\n{string.Join("\n", ihlaller)}");
        }
        finally
        {
            await sayfa.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task Denetimler_bozuk_sayfayi_yakalar()
    {
        var sayfa = await app.SayfaAsync(genislik: 320, yukseklik: 640);
        try
        {
            await sayfa.GotoAsync("/");
            await sayfa.EvaluateAsync("() => { const i = document.createElement('img'); i.src = '/favicon.ico'; document.querySelector('main').append(i); const k = document.createElement('div'); k.style.width = '600px'; k.style.height = '10px'; document.querySelector('main').append(k); }");
            var sonuc = await sayfa.RunAxe(Wcag);
            Assert.Contains(sonuc.Violations, v => v.Id == "image-alt");
            Assert.True(await sayfa.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        }
        finally
        {
            await sayfa.Context.CloseAsync();
        }
    }

    [Theory]
    [MemberData(nameof(Sayfalar))]
    public async Task Sayfa_320_piksel_genislikte_yatay_tasma_yapmaz(string? giris, string yol)
    {
        var sayfa = await app.SayfaAsync(giris, genislik: 320, yukseklik: 640);
        try
        {
            await sayfa.GotoAsync(yol);
            var tasanlar = await sayfa.EvaluateAsync<string[]>(
                """
                () => {
                  const w = document.documentElement.clientWidth;
                  if (document.documentElement.scrollWidth <= w) return [];
                  const adlar = [...document.querySelectorAll('body *')]
                    .filter(e => e.getBoundingClientRect().right > w + 1 && !e.closest('.tablo-kap'))
                    .slice(0, 8)
                    .map(e => e.tagName.toLowerCase() + (e.id ? '#' + e.id : '') + (e.className ? '.' + String(e.className).replace(/ +/g, '.') : ''));
                  return adlar.length ? adlar : ['scrollWidth=' + document.documentElement.scrollWidth];
                }
                """);
            Assert.True(tasanlar.Length == 0, $"{yol} 320 px'te taşıyor: {string.Join(", ", tasanlar)}");
        }
        finally
        {
            await sayfa.Context.CloseAsync();
        }
    }
}
