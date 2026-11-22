using System.Net;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using CihazServis.Tests.Altyapi;
using CihazServis.Tests.Birim;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class ServisTalebiTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private UygulamaFabrikasi _f = null!;

    public Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task<int> TalepSayisi() => _f.VeriAsync(db => db.ServisTalepleri.CountAsync());

    private Task<ServisTalebi> SonTalep() => _f.VeriAsync(db => db.ServisTalepleri.Include(t => t.Ekler).Include(t => t.Sorumlu)
        .OrderByDescending(t => t.Id).FirstAsync());

    [Fact]
    public async Task Talep_acilir_numara_verilir_kurala_gore_atanir_ve_eposta_kuyruga_girer()
    {
        var c = _f.Istemci();
        var yanit = await TalepIstemcisi.GonderAsync(c, TalepIstemcisi.GecerliAlanlar("dmo a240 0001"));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/servis/talep/alindi", yanit.Headers.Location?.OriginalString);

        var t = await SonTalep();
        Assert.Matches("^SR-2026-[0-9]{6}$", t.No);
        Assert.True(t.Dogrulandi);
        Assert.Equal("DMOA2400001", t.SeriNo);
        Assert.Equal("personel@servis.test", t.Sorumlu.Eposta); // kural: Arıza + A100
        Assert.Equal("5551112233", t.Telefon);
        Assert.Equal(AydinlatmaMetni.Surum, t.AydinlatmaSurumu);
        Assert.Equal(UygulamaFabrikasi.Baslangic, t.AydinlatmaOnayZamani);
        // Cuma açılan, Plus seviyeli cihazın arızası: 2 iş günü -> Salı.
        Assert.Equal(new DateOnly(2026, 11, 24), t.HedefTarih);
        Assert.Contains(t.Id, _f.Kuyruk.AlinanTalepler);
        Assert.True(await _f.VeriAsync(db => db.DenetimKayitlari.AnyAsync(d => d.Islem == "TalepOlusturuldu" && d.VarlikNo == t.No)));

        // Yönlendirilen sayfada numara görünür, sayfa yenilenirse yeni talep açılmaz.
        var alindi = await c.GetStringAsync("/servis/talep/alindi");
        Assert.Contains(t.No, alindi);
        var yenile = await c.GetAsync("/servis/talep/alindi");
        Assert.Equal(HttpStatusCode.Redirect, yenile.StatusCode);
        Assert.Equal(3, await TalepSayisi());
    }

    [Fact]
    public async Task Kayitsiz_seri_ile_de_talep_alinir_kural_yoksa_yoneticiye_duser()
    {
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar("DMO-A240-9999", "Diger"));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var t = await SonTalep();
        Assert.False(t.Dogrulandi);
        Assert.Null(t.CihazId);
        Assert.Equal("yonetici@servis.test", t.Sorumlu.Eposta);
        Assert.Equal(new DateOnly(2026, 11, 27), t.HedefTarih); // Diğer: 5 iş günü
    }

    [Fact]
    public async Task Aydinlatma_onaylanmadan_talep_alinmaz()
    {
        var alanlar = TalepIstemcisi.GecerliAlanlar();
        alanlar.Remove("AydinlatmaOnay");
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), alanlar);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("aydınlatma metnini okuduğunuzu onaylayın", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(2, await TalepSayisi());
    }

    [Fact]
    public async Task Eski_surum_aydinlatma_onayi_kabul_edilmez()
    {
        var alanlar = TalepIstemcisi.GecerliAlanlar();
        alanlar["AydinlatmaSurumu"] = "0.9";
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), alanlar);
        Assert.Contains("Aydınlatma metni güncellendi", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(2, await TalepSayisi());
    }

    [Fact]
    public async Task Gecersiz_telefon_reddedilir()
    {
        var alanlar = TalepIstemcisi.GecerliAlanlar();
        alanlar["Telefon"] = "12345";
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), alanlar);
        Assert.Contains("0555 123 45 67 biçiminde", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(2, await TalepSayisi());
    }

    [Fact]
    public async Task Uzantisi_pdf_olan_calistirilabilir_dosya_reddedilir_ve_diske_yazilmaz()
    {
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar(),
            new TestEki("foto.jpg", DosyaImzasiTestleri.Jpeg), new TestEki("fatura.pdf", DosyaImzasiTestleri.Exe));
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("fatura.pdf dosyasının türü tanınmadı", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(2, await TalepSayisi());
        Assert.False(Directory.Exists(_f.DepoKlasoru) && Directory.EnumerateFiles(_f.DepoKlasoru, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Sinirdan_buyuk_dosya_reddedilir()
    {
        var buyuk = new byte[ServisTalebiServisi.AzamiEkBoyutu + 1];
        DosyaImzasiTestleri.Jpeg.CopyTo(buyuk, 0);
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar(), new TestEki("buyuk.jpg", buyuk));
        Assert.Contains("buyuk.jpg dosyası boş ya da 10 MB", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(2, await TalepSayisi());
    }

    [Fact]
    public async Task Dorduncu_ek_reddedilir()
    {
        var ek = new TestEki("a.png", DosyaImzasiTestleri.Png);
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar(), ek, ek, ek, ek);
        Assert.Contains("En fazla 3 dosya", await yanit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Gecerli_ekler_imzadan_tespit_edilen_turle_rastgele_adla_saklanir()
    {
        var yanit = await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar(),
            new TestEki("ariza ekranı.JPG", DosyaImzasiTestleri.Jpeg), new TestEki("../../servis formu.pdf", DosyaImzasiTestleri.Pdf));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var t = await SonTalep();
        Assert.Equal(2, t.Ekler.Count);
        Assert.Contains(t.Ekler, e => e.IcerikTuru == "image/jpeg" && e.OrijinalAd == "ariza ekranı.JPG");
        Assert.Contains(t.Ekler, e => e.IcerikTuru == "application/pdf" && e.OrijinalAd == "servis formu.pdf");
        Assert.All(t.Ekler, e => Assert.Matches("^[0-9a-f]{32}$", e.DepoAdi));
        Assert.Equal(2, Directory.EnumerateFiles(_f.DepoKlasoru, "*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task Tuzak_alan_doluysa_basarili_gorunur_ama_kayit_acilmaz()
    {
        var alanlar = TalepIstemcisi.GecerliAlanlar();
        alanlar["Website"] = "http://spam.example";
        var c = _f.Istemci();
        var yanit = await TalepIstemcisi.GonderAsync(c, alanlar);
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Contains("Talebiniz alındı", await c.GetStringAsync("/servis/talep/alindi"));
        Assert.Equal(2, await TalepSayisi());
        Assert.Empty(_f.Kuyruk.AlinanTalepler);
    }

    [Fact]
    public async Task Eszamanli_numara_istekleri_hep_farkli_numara_alir()
    {
        var gorevler = Enumerable.Range(0, 30).Select(async _ =>
        {
            await using var s = _f.Services.CreateAsyncScope();
            return await s.ServiceProvider.GetRequiredService<ITalepNoUretici>().SonrakiAsync(2027);
        });
        var numaralar = await Task.WhenAll(gorevler);
        Assert.Equal(30, numaralar.Distinct().Count());
        Assert.All(numaralar, n => Assert.StartsWith("SR-2027-", n));
    }

    [Fact]
    public async Task Hareket_ve_denetim_tablolari_guncellenemez_silinemez()
    {
        foreach (var sqlCumlesi in new[]
                 {
                     "UPDATE servis.TalepHareketleri SET MusteriNotu = 'x'",
                     "DELETE FROM servis.TalepHareketleri",
                     "UPDATE servis.DenetimKayitlari SET Islem = 'x'",
                 })
        {
            var hata = await Assert.ThrowsAnyAsync<Exception>(() => _f.VeriAsync(db => db.Database.ExecuteSqlRawAsync(sqlCumlesi)));
            Assert.Contains("yalnız eklemeye açıktır", hata.Message);
        }
    }
}
