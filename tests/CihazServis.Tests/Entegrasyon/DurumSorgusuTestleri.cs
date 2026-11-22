using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CihazServis.Infrastructure.Veri;
using CihazServis.Tests.Altyapi;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class DurumSorgusuTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private UygulamaFabrikasi _f = null!;

    public Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task<HttpResponseMessage> Sorgula(string no, string son4)
        => _f.Istemci().PostAsJsonAsync("/api/v1/servis/durum", new { talepNo = no, telefonSon4 = son4 });

    [Fact]
    public async Task Dogru_bilgilerle_zaman_cizelgesi_doner_kisisel_veri_ve_ic_not_donmez()
    {
        var yanit = await Sorgula("sr 2026 000001", "1234");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store", yanit.Headers.CacheControl?.ToString());
        var ham = await yanit.Content.ReadAsStringAsync();
        var j = JsonDocument.Parse(ham).RootElement;
        Assert.Equal(DemoVeriTohumlayici.DemoTalepAcik, j.GetProperty("no").GetString());
        Assert.Equal("serviste", j.GetProperty("durum").GetString());
        Assert.Equal("Demo Cihaz A100", j.GetProperty("modelAdi").GetString());
        // Fabrikanın saati 20 Kasım 2026 Cuma: tohumlanan açık talebin hedefi 2 iş günü sonrası, 24 Kasım Salı.
        Assert.Equal("2026-11-24", j.GetProperty("tahminiTarih").GetString());
        var cizelge = j.GetProperty("cizelge").EnumerateArray().Select(a => a.GetProperty("durum").GetString()).ToList();
        Assert.Equal(["alindi", "inceleniyor", "parcaBekleniyor", "serviste"], cizelge);
        Assert.Contains("Değişecek elektrot plakası sipariş edildi.", ham);

        // Ham gövdede kişisel veri ya da iç not geçmez.
        foreach (var yasak in new[] { "Ayşe", "Yılmaz", "5550001234", "musteri@", "Örnek Klinik", "Tedarikçi", "elektrot temas hatası", "DMOA2400001", "DMO-A240" })
        {
            Assert.DoesNotContain(yasak, ham, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Kapali_talepte_tahmini_tarih_yerine_kapanis_tarihi_doner()
    {
        var j = await (await Sorgula(DemoVeriTohumlayici.DemoTalepKapali, "5678")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tamamlandi", j.GetProperty("durum").GetString());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("tahminiTarih").ValueKind);
        Assert.Equal("2026-11-12", j.GetProperty("kapanisTarihi").GetString());
    }

    [Fact]
    public async Task Olmayan_numara_ile_yanlis_hane_ayni_yaniti_verir()
    {
        var olmayan = await Sorgula("SR-2026-999999", "1234");
        var yanlis = await Sorgula(DemoVeriTohumlayici.DemoTalepAcik, "0000");
        Assert.Equal(HttpStatusCode.NotFound, olmayan.StatusCode);
        Assert.Equal(olmayan.StatusCode, yanlis.StatusCode);
        Assert.Equal(await olmayan.Content.ReadAsStringAsync(), await yanlis.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bes_hatali_denemede_numara_kilitlenir_dogru_hane_de_reddedilir_sure_dolunca_acilir()
    {
        for (var i = 0; i < 5; i++)
        {
            // Farklı yazımlar aynı numaraya sayılır: kilit normalize edilmiş numaraya bağlıdır.
            var yazim = i % 2 == 0 ? "SR-2026-000001" : "sr2026000001";
            Assert.Equal(HttpStatusCode.NotFound, (await Sorgula(yazim, "9999")).StatusCode);
        }

        var kilitli = await Sorgula(DemoVeriTohumlayici.DemoTalepAcik, "1234");
        Assert.Equal(HttpStatusCode.TooManyRequests, kilitli.StatusCode);
        var j = await kilitli.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("kilitli", j.GetProperty("hata").GetString());

        // Başka bir talep bu kilitten etkilenmez.
        Assert.Equal(HttpStatusCode.OK, (await Sorgula(DemoVeriTohumlayici.DemoTalepKapali, "5678")).StatusCode);

        _f.Saat.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Sorgula(DemoVeriTohumlayici.DemoTalepAcik, "1234")).StatusCode);
        _f.Saat.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await Sorgula(DemoVeriTohumlayici.DemoTalepAcik, "1234")).StatusCode);
        Assert.False(await _f.VeriAsync(db => db.DurumSorguKilitleri.AnyAsync()));
    }

    [Fact]
    public async Task Olmayan_numara_da_kilitlenir_boylece_kilit_numaranin_varligini_ele_vermez()
    {
        for (var i = 0; i < 5; i++)
        {
            await Sorgula("SR-2026-555555", "1111");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Sorgula("SR-2026-555555", "1111")).StatusCode);
    }

    [Fact]
    public async Task Eszamanli_hatali_denemeler_kaybolmaz()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Sorgula("SR-2026-444444", "1111")));
        var kilit = await _f.VeriAsync(db => db.DurumSorguKilitleri.SingleAsync(k => k.TalepNo == "SR-2026-444444"));
        Assert.Equal(8, kilit.HataliDeneme);
        Assert.NotNull(kilit.KilitBitis);
    }

    [Fact]
    public async Task Bicimsiz_girdi_400_doner_ve_kilit_kaydi_acmaz()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Sorgula("12345", "1234")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Sorgula("SR-2026-000001", "12")).StatusCode);
        Assert.False(await _f.VeriAsync(db => db.DurumSorguKilitleri.AnyAsync()));
    }

    [Fact]
    public async Task Temizleme_isi_suresi_dolan_kilitleri_siler()
    {
        for (var i = 0; i < 5; i++)
        {
            await Sorgula("SR-2026-333333", "1111");
        }

        await Sorgula("SR-2026-222222", "1111");
        _f.Saat.Advance(TimeSpan.FromMinutes(16));
        await using var s = _f.Services.CreateAsyncScope();
        var silinen = await s.ServiceProvider.GetRequiredService<CihazServis.Application.Servis.DurumKilidiTemizleyici>().TemizleAsync();
        Assert.Equal(1, silinen);
        Assert.True(await _f.VeriAsync(db => db.DurumSorguKilitleri.AnyAsync(k => k.TalepNo == "SR-2026-222222")));
    }

    [Fact]
    public async Task Sayfadan_sorgu_post_ile_yapilir_ve_kisisel_veri_gostermez()
    {
        var c = _f.Istemci();
        var sayfa = await c.GetStringAsync("/servis/durum");
        var yanit = await c.PostAsync("/servis/durum", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TalepNo"] = DemoVeriTohumlayici.DemoTalepAcik,
            ["TelefonSon4"] = "1234",
            ["__RequestVerificationToken"] = Yardimci.AntiforgeryAnahtari(sayfa),
        }));
        var html = await yanit.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("data-sonuc=\"bulundu\"", html);
        Assert.Contains("Parça bekleniyor", html);
        Assert.DoesNotContain("Ayşe", html);
        Assert.DoesNotContain("value=\"1234\"", html);
    }
}
