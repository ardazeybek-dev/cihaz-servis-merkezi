using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CihazServis.Application.Varliklar;
using CihazServis.Infrastructure.Veri;
using CihazServis.Tests.Altyapi;
using CihazServis.Tests.Birim;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class YonetimTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private const string Personel = "personel@servis.test";
    private const string Yonetici = "yonetici@servis.test";
    private const string KlinikMusterisi = "musteri@ornek-klinik.test";

    private UygulamaFabrikasi _f = null!;

    public Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<HttpClient> Girisli(string eposta)
    {
        var c = _f.Istemci();
        await Yardimci.GirisYap(c, eposta);
        return c;
    }

    private static async Task<HttpResponseMessage> DurumGonder(HttpClient c, string no, string yeni, string beklenen, string? musteriNotu = null, string? icNot = null)
    {
        var sayfa = await c.GetStringAsync($"/yonetim/talep/{no}");
        return await c.PostAsync($"/yonetim/talep/{no}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["YeniDurum"] = yeni,
            ["BeklenenDurum"] = beklenen,
            ["MusteriNotu"] = musteriNotu ?? "",
            ["IcNot"] = icNot ?? "",
            ["__RequestVerificationToken"] = Yardimci.AntiforgeryAnahtari(sayfa),
        }));
    }

    [Fact]
    public async Task Personel_durumu_degistirir_musteri_sorguda_gorur_ic_not_gizli_kalir()
    {
        var c = await Girisli(Personel);
        var yanit = await DurumGonder(c, DemoVeriTohumlayici.DemoTalepAcik, "Tamamlandi", "Serviste", "Elektrot plakası değiştirildi.", "Eski parça iade kutusunda.");
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);

        var hareket = await _f.VeriAsync(db => db.TalepHareketleri.OrderByDescending(h => h.Id).FirstAsync());
        Assert.Equal(TalepDurumu.Tamamlandi, hareket.YeniDurum);
        Assert.Contains(hareket.Id, _f.Kuyruk.DurumDegisiklikleri);
        Assert.True(await _f.VeriAsync(db => db.DenetimKayitlari.AnyAsync(d => d.Islem == "DurumDegisti" && d.VarlikNo == DemoVeriTohumlayici.DemoTalepAcik)));

        var sorgu = await _f.Istemci().PostAsJsonAsync("/api/v1/servis/durum", new { talepNo = DemoVeriTohumlayici.DemoTalepAcik, telefonSon4 = "1234" });
        var ham = await sorgu.Content.ReadAsStringAsync();
        Assert.Equal("tamamlandi", JsonDocument.Parse(ham).RootElement.GetProperty("durum").GetString());
        Assert.Contains("Elektrot plakası değiştirildi.", ham);
        Assert.DoesNotContain("iade kutusunda", ham);
    }

    [Fact]
    public async Task Gecersiz_durum_gecisi_reddedilir()
    {
        var c = await Girisli(Personel);
        var yanit = await DurumGonder(c, DemoVeriTohumlayici.DemoTalepKapali, "Inceleniyor", "Tamamlandi");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("Tamamlandı durumundaki talep İnceleniyor durumuna geçirilemez", await yanit.Content.ReadAsStringAsync());
        Assert.Equal(TalepDurumu.Tamamlandi, await _f.VeriAsync(db => db.ServisTalepleri.Where(t => t.No == DemoVeriTohumlayici.DemoTalepKapali).Select(t => t.Durum).FirstAsync()));
    }

    [Fact]
    public async Task Bayat_formla_yapilan_degisiklik_reddedilir()
    {
        var c = await Girisli(Personel);
        var yanit = await DurumGonder(c, DemoVeriTohumlayici.DemoTalepAcik, "Tamamlandi", "Inceleniyor");
        Assert.Contains("başka biri tarafından güncellendi", await yanit.Content.ReadAsStringAsync());
        Assert.Empty(_f.Kuyruk.DurumDegisiklikleri);
    }

    [Fact]
    public async Task Yalniz_ic_not_eklemek_musteriye_eposta_gondermez()
    {
        var c = await Girisli(Personel);
        var yanit = await DurumGonder(c, DemoVeriTohumlayici.DemoTalepAcik, "Serviste", "Serviste", icNot: "Müşteri yarın aranacak.");
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Empty(_f.Kuyruk.DurumDegisiklikleri);
    }

    [Fact]
    public async Task Ek_yalniz_personele_iner_tur_imzadan_gelir_indirme_denetlenir()
    {
        await TalepIstemcisi.GonderAsync(_f.Istemci(), TalepIstemcisi.GecerliAlanlar(), new TestEki("ekran.jpg", DosyaImzasiTestleri.Jpeg));
        var ek = await _f.VeriAsync(db => db.TalepEkleri.OrderByDescending(e => e.Id)
            .Select(e => new { e.Id, No = db.ServisTalepleri.Where(t => t.Id == e.TalepId).Select(t => t.No).First() })
            .FirstAsync());
        var (no, ekId) = (ek.No, ek.Id);
        var adres = $"/yonetim/talep/{no}?handler=Ek&ekId={ekId}";

        var anonim = await _f.Istemci().GetAsync(adres);
        Assert.Equal(HttpStatusCode.Redirect, anonim.StatusCode);
        Assert.StartsWith("https://localhost/giris", anonim.Headers.Location!.ToString());

        var musteri = await (await Girisli(KlinikMusterisi)).GetAsync(adres);
        Assert.Equal(HttpStatusCode.Redirect, musteri.StatusCode);
        Assert.Contains("/durum/403", musteri.Headers.Location!.ToString());

        var personel = await (await Girisli(Personel)).GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, personel.StatusCode);
        Assert.Equal("image/jpeg", personel.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", personel.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("sandbox", personel.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(DosyaImzasiTestleri.Jpeg, await personel.Content.ReadAsByteArrayAsync());
        Assert.True(await _f.VeriAsync(db => db.DenetimKayitlari.AnyAsync(d => d.Islem == "EkIndirildi" && d.VarlikNo == no)));

        // Ek başka bir talebin numarasıyla istenirse bulunmaz.
        var baska = await (await Girisli(Personel)).GetAsync($"/yonetim/talep/{DemoVeriTohumlayici.DemoTalepKapali}?handler=Ek&ekId={ekId}");
        Assert.Equal(HttpStatusCode.NotFound, baska.StatusCode);
    }

    [Fact]
    public async Task Yonetim_ekrani_musteriye_ve_ziyaretciye_kapali()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await _f.Istemci().GetAsync("/yonetim")).StatusCode);
        var musteri = await (await Girisli(KlinikMusterisi)).GetAsync("/yonetim");
        Assert.Contains("/durum/403", musteri.Headers.Location!.ToString());
        var personel = await (await Girisli(Personel)).GetStringAsync("/yonetim");
        Assert.Contains(DemoVeriTohumlayici.DemoTalepAcik, personel);
        Assert.DoesNotContain(DemoVeriTohumlayici.DemoTalepKapali, personel); // varsayılan: yalnız açıklar
    }

    [Fact]
    public void Testlerde_hangfire_kurulmaz()
    {
        _ = _f.Services;
        Assert.Null(_f.Services.GetService<Hangfire.IBackgroundJobClient>());
    }

    [Fact]
    public async Task Menudeki_cikis_dugmesi_oturumu_kapatir()
    {
        var c = await Girisli(Personel);
        var sayfa = await c.GetStringAsync("/yonetim");
        var yanit = await c.PostAsync("/cikis", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Yardimci.AntiforgeryAnahtari(sayfa),
        }));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/yonetim")).StatusCode);
    }

    [Fact]
    public async Task Denetim_gunlugu_yalniz_yoneticiye_acik()
    {
        var personel = await (await Girisli(Personel)).GetAsync("/yonetim/denetim");
        Assert.Contains("/durum/403", personel.Headers.Location!.ToString());
        var yonetici = await (await Girisli(Yonetici)).GetAsync("/yonetim/denetim");
        Assert.Equal(HttpStatusCode.OK, yonetici.StatusCode);
    }

    [Fact]
    public async Task Musteri_kendi_cihazini_gorur_baska_kurumun_cihazi_404()
    {
        var (kendi, baska) = await _f.VeriAsync(async db => (
            await db.Cihazlar.Where(x => x.SeriNo == "DMOA2400001").Select(x => x.Id).FirstAsync(),
            await db.Cihazlar.Where(x => x.SeriNo == "DMOB2500003").Select(x => x.Id).FirstAsync()));
        var c = await Girisli(KlinikMusterisi);

        var liste = await c.GetStringAsync("/musteri/cihazlar");
        Assert.Contains("DMO-A240-0001", liste);
        Assert.DoesNotContain("DMO-B250-0003", liste);

        var kendiSayfa = await c.GetAsync($"/musteri/cihazlar/{kendi}");
        Assert.Equal(HttpStatusCode.OK, kendiSayfa.StatusCode);
        Assert.Contains(DemoVeriTohumlayici.DemoTalepAcik, await kendiSayfa.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/musteri/cihazlar/{baska}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/musteri/cihazlar/999999")).StatusCode);
    }

    [Fact]
    public async Task Sertifika_iptali_denetim_gunlugune_yazilir()
    {
        // Eğitim akışı ayrı testlerde sınanıyor; burada servisin iptal sonrası izini kontrol ediyoruz.
        var no = await _f.VeriAsync(async db =>
        {
            var k = await db.Kullanicilar.FirstAsync(x => x.Eposta == KlinikMusterisi);
            var kurs = await db.Kurslar.FirstAsync();
            var s = new Sertifika { No = CihazServis.Application.Egitim.SertifikaServisi.YeniNo(), KullaniciId = k.Id, KursId = kurs.Id, AdSoyad = k.AdSoyad, VerilisZamani = UygulamaFabrikasi.Baslangic };
            s.Hareketler.Add(new SertifikaHareketi { Tur = SertifikaHareketTuru.Verildi, Zaman = UygulamaFabrikasi.Baslangic });
            db.Sertifikalar.Add(s);
            await db.SaveChangesAsync();
            return s.No;
        });

        var c = await Girisli(Personel);
        var sayfa = await c.GetStringAsync("/yonetim/sertifikalar");
        Assert.Contains(no, sayfa);
        var yanit = await c.PostAsync("/yonetim/sertifikalar?handler=Iptal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["No"] = no,
            ["Gerekce"] = "Yanlış kişiye verildi.",
            ["__RequestVerificationToken"] = Yardimci.AntiforgeryAnahtari(sayfa),
        }));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.True(await _f.VeriAsync(db => db.DenetimKayitlari.AnyAsync(d => d.Islem == "SertifikaIptal" && d.VarlikNo == no)));
        Assert.Contains("İptal edildi", await c.GetStringAsync("/yonetim/sertifikalar"));

        // Karekodu okutan herkes, girişsiz doğrulama sayfasında sertifikanın artık geçersiz olduğunu görür.
        using var ziyaretci = _f.CreateClient();
        var dogrulama = await ziyaretci.GetStringAsync($"/sertifika/{no}");
        Assert.Contains("data-sertifika=\"iptal\"", dogrulama);
        Assert.Contains("İptal tarihi", dogrulama);
    }
}
