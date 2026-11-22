using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace CihazServis.E2E;

/// <summary>
/// Müşteri talep açar, e-posta gerçekten SMTP'ye düşer, personel durumu değiştirir,
/// müşteri durum sorgusunda yeni durumu ve notu görür.
/// </summary>
[Collection(UygulamaKoleksiyonu.Ad)]
public sealed class ServisAkisiTestleri(UygulamaSureci app)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 0, 0];

    private async Task<string> EpostaBekleAsync(string konuParcasi)
    {
        using var http = new HttpClient();
        for (var i = 0; i < 40; i++)
        {
            var j = await http.GetFromJsonAsync<JsonElement>($"{app.EpostaApi}/api/messages?pageSize=50");
            foreach (var m in j.GetProperty("results").EnumerateArray())
            {
                var konu = m.GetProperty("subject").GetString() ?? "";
                if (konu.Contains(konuParcasi, StringComparison.Ordinal))
                {
                    return await http.GetStringAsync($"{app.EpostaApi}/api/messages/{m.GetProperty("id").GetString()}/plaintext");
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"'{konuParcasi}' konulu e-posta gelmedi.");
    }

    [Fact]
    public async Task Talep_acilir_eposta_gider_personel_gunceller_musteri_sorguda_gorur()
    {
        // 1) Müşteri, garanti sorgusundan gelen bağlantıyla formu açar: seri numarası dolu gelir.
        var musteri = await app.SayfaAsync(genislik: 390, yukseklik: 844);
        await musteri.GotoAsync("/servis/talep?seriNo=DMO-A240-0001");
        Assert.Equal("DMO-A240-0001", await musteri.InputValueAsync("#SeriNo"));
        await musteri.SelectOptionAsync("#Konu", "Ariza");
        await musteri.FillAsync("#Aciklama", "Ölçüm ekranı açılışta donuyor, E-30 hata kodu görünüyor.");
        await musteri.SetInputFilesAsync("#Ekler", new FilePayload { Name = "ekran.jpg", MimeType = "image/jpeg", Buffer = Jpeg });
        await musteri.FillAsync("#AdSoyad", "Elif Şahin");
        await musteri.FillAsync("#Telefon", "0532 765 43 21");
        await musteri.FillAsync("#Eposta", "elif@ornek.test");
        await musteri.CheckAsync("#AydinlatmaOnay");
        await musteri.ClickAsync("main form button[type=submit]");
        await musteri.WaitForURLAsync("**/servis/talep/alindi");
        var no = (await musteri.InnerTextAsync("#talep-no")).Trim();
        Assert.Matches("^SR-2026-[0-9]{6}$", no);

        // 2) "Talebiniz alındı" e-postası SMTP sunucusuna ulaşır.
        var ilkPosta = await EpostaBekleAsync($"Servis talebiniz alındı: {no}");
        Assert.Contains("Merhaba Elif Şahin", ilkPosta, StringComparison.Ordinal);
        Assert.Contains("/servis/durum", ilkPosta, StringComparison.Ordinal);

        // 3) Personel kuyrukta talebi görür, eki açabilir ve durumu günceller.
        var personel = await app.SayfaAsync("personel@servis.test");
        await personel.GotoAsync("/yonetim");
        await personel.ClickAsync($"[data-talep='{no}'] a");
        await personel.WaitForURLAsync($"**/yonetim/talep/{no}");
        var ekIstegi = await personel.Context.APIRequest.GetAsync(await personel.GetAttributeAsync("[data-ek]", "href") ?? "");
        Assert.Equal(200, ekIstegi.Status);
        Assert.Equal("image/jpeg", ekIstegi.Headers["content-type"]);
        await personel.SelectOptionAsync("#YeniDurum", "Inceleniyor");
        await personel.FillAsync("#MusteriNotu", "Cihazınız uzaktan incelendi, yarın teknisyen gelecek.");
        await personel.FillAsync("#IcNot", "Anakart arızası olabilir.");
        await personel.ClickAsync("main form[method=post] button[type=submit]");
        await personel.WaitForSelectorAsync("[data-durum='Inceleniyor']");

        // 4) Müşteri talep numarası ve telefonun son 4 hanesiyle durumu görür; iç not görünmez.
        await musteri.GotoAsync("/servis/durum");
        await musteri.FillAsync("#TalepNo", no.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal));
        await musteri.FillAsync("#TelefonSon4", "4321");
        await musteri.ClickAsync("main form button[type=submit]");
        var sonuc = musteri.Locator("[data-sonuc='bulundu']");
        await sonuc.WaitForAsync();
        Assert.Equal("Inceleniyor", await sonuc.GetAttributeAsync("data-durum"));
        var metin = await sonuc.InnerTextAsync();
        Assert.Contains("yarın teknisyen gelecek", metin, StringComparison.Ordinal);
        Assert.DoesNotContain("Anakart", metin, StringComparison.Ordinal);
        Assert.DoesNotContain("Elif", await musteri.ContentAsync(), StringComparison.Ordinal);

        // 5) Durum değişikliği e-postası da gider, iç not içermez.
        var ikinciPosta = await EpostaBekleAsync($"Talebinizin durumu: İnceleniyor ({no})");
        Assert.Contains("yarın teknisyen gelecek", ikinciPosta, StringComparison.Ordinal);
        Assert.DoesNotContain("Anakart", ikinciPosta, StringComparison.Ordinal);

        await musteri.Context.CloseAsync();
        await personel.Context.CloseAsync();
    }
}
