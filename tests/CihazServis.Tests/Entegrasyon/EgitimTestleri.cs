using System.Net;
using CihazServis.Tests.Altyapi;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class EgitimTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private UygulamaFabrikasi _f = null!;
    private EgitimIstemcisi _e = null!;
    private int _ders1;
    private int _ders2;

    public async Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        _e = await EgitimIstemcisi.GirisAsync(_f);
        _ders1 = await _e.DersIdAsync(1);
        _ders2 = await _e.DersIdAsync(2);
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<Guid> Oturum(int dersId) => (await _e.OturumAsync(dersId)).GetProperty("oturumId").GetGuid();

    [Fact]
    public async Task Gercek_hizda_izlenen_video_tamamlanir()
    {
        var o = await Oturum(_ders1);
        for (var t = 0; t < 40; t += 10)
        {
            _f.Saat.Advance(TimeSpan.FromSeconds(10));
            var (durum, j) = await _e.IzlemeAsync(_ders1, o, t, t + 10);
            Assert.Equal(HttpStatusCode.OK, durum);
            Assert.Equal(t + 10 >= 38, j.GetProperty("tamamlandi").GetBoolean());
        }
    }

    [Fact]
    public async Task Ileri_atlayan_konum_bildirimi_reddedilir()
    {
        var o = await Oturum(_ders1);
        _f.Saat.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.OK, (await _e.IzlemeAsync(_ders1, o, 0, 10)).Durum);

        _f.Saat.Advance(TimeSpan.FromSeconds(10));
        var (durum, j) = await _e.IzlemeAsync(_ders1, o, 30, 40);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, durum);
        Assert.Equal("ileriAtlama", j.GetProperty("hata").GetString());
        Assert.Equal(10, j.GetProperty("durum").GetProperty("enIleriSaniye").GetDouble());

        var i = await _f.VeriAsync(db => db.DersIlerlemeleri.SingleAsync(x => x.DersId == _ders1));
        Assert.Equal(10, i.IzlenenSaniye);
        Assert.Null(i.VideoTamamlandi);
    }

    [Fact]
    public async Task Istemcinin_bitti_demesine_guvenilmez()
    {
        var o = await Oturum(_ders1);
        _f.Saat.Advance(TimeSpan.FromSeconds(1));
        var (durum, j) = await _e.IzlemeAsync(_ders1, o, 0, 40);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, durum);
        Assert.Equal("hizliIzleme", j.GetProperty("hata").GetString());
    }

    [Fact]
    public async Task Kucuk_parcalarla_hizlandirma_da_yakalanir()
    {
        // Her bildirim tek başına toleransın içinde ama toplamı gerçek süreyi aşıyor: 2 kat hız.
        var o = await Oturum(_ders1);
        var reddedildi = false;
        for (var t = 0.0; t < 40 && !reddedildi; t += 2)
        {
            _f.Saat.Advance(TimeSpan.FromSeconds(1));
            reddedildi = (await _e.IzlemeAsync(_ders1, o, t, t + 2)).Durum == HttpStatusCode.UnprocessableEntity;
        }

        Assert.True(reddedildi);
        var i = await _f.VeriAsync(db => db.DersIlerlemeleri.SingleAsync(x => x.DersId == _ders1));
        Assert.Null(i.VideoTamamlandi);
    }

    [Fact]
    public async Task Geri_sarip_tekrar_izlemek_serbesttir()
    {
        var o = await Oturum(_ders1);
        _f.Saat.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.OK, (await _e.IzlemeAsync(_ders1, o, 0, 20)).Durum);
        _f.Saat.Advance(TimeSpan.FromSeconds(10));
        var (durum, j) = await _e.IzlemeAsync(_ders1, o, 5, 15);
        Assert.Equal(HttpStatusCode.OK, durum);
        Assert.Equal(0.5, j.GetProperty("izlenenOran").GetDouble());
    }

    [Fact]
    public async Task Ikinci_sekme_acilinca_ilk_oturumun_bildirimleri_reddedilir()
    {
        var ilk = await Oturum(_ders1);
        await Oturum(_ders1);
        _f.Saat.Advance(TimeSpan.FromSeconds(10));
        var (durum, j) = await _e.IzlemeAsync(_ders1, ilk, 0, 10);
        Assert.Equal(HttpStatusCode.Conflict, durum);
        Assert.Equal("baskaOturum", j.GetProperty("hata").GetString());
    }

    [Fact]
    public async Task Kilitli_derse_adres_degistirerek_gecilemez()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _e.PostAsync($"/api/v1/egitim/dersler/{_ders2}/oturum")).Durum);
        Assert.Equal(HttpStatusCode.Forbidden, (await _e.Http.GetAsync($"/egitim/ders/{_ders2}")).StatusCode);
    }

    [Fact]
    public async Task Video_bitmeden_quiz_acilmaz()
    {
        var (durum, j) = await _e.PostAsync($"/api/v1/egitim/dersler/{_ders1}/quiz");
        Assert.Equal(HttpStatusCode.Forbidden, durum);
        Assert.Equal("videoTamamlanmadi", j.GetProperty("hata").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _e.Http.GetAsync($"/egitim/quiz/{_ders1}")).StatusCode);
    }

    [Fact]
    public async Task Quiz_yanitinda_dogru_cevap_bilgisi_yoktur()
    {
        await _e.VideoyuIzleAsync(_ders1);
        var q = await _e.QuizAsync(_ders1);
        Assert.Equal(5, q.GetProperty("sorular").GetArrayLength());
        var ham = q.GetRawText();
        Assert.DoesNotContain("dogru", ham, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correct", ham, StringComparison.OrdinalIgnoreCase);
        foreach (var s in q.GetProperty("sorular").EnumerateArray())
        {
            foreach (var secenek in s.GetProperty("secenekler").EnumerateArray())
            {
                Assert.Equal(["id", "metin"], secenek.EnumerateObject().Select(p => p.Name).ToArray());
            }
        }

        // Sayfa hâli de aynı: HTML'de doğru cevabı ele veren bir işaret yok.
        var html = await _e.Http.GetStringAsync($"/egitim/quiz/{_ders1}");
        Assert.DoesNotContain("dogru", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sayfa_yenilenince_ayni_sorular_gelir()
    {
        await _e.VideoyuIzleAsync(_ders1);
        var a = await _e.QuizAsync(_ders1);
        var b = await _e.QuizAsync(_ders1);
        Assert.Equal(a.GetProperty("denemeId").GetGuid(), b.GetProperty("denemeId").GetGuid());
        Assert.Equal(a.GetProperty("sorular").GetRawText(), b.GetProperty("sorular").GetRawText());
    }

    [Fact]
    public async Task Quiz_gecilince_sonraki_ders_acilir()
    {
        await _e.DersiBitirAsync(1);
        Assert.Equal(HttpStatusCode.OK, (await _e.PostAsync($"/api/v1/egitim/dersler/{_ders2}/oturum")).Durum);
    }

    [Fact]
    public async Task Uc_hak_bitince_video_sifirlanir()
    {
        await _e.VideoyuIzleAsync(_ders1);
        System.Text.Json.JsonElement son = default;
        for (var i = 0; i < 3; i++)
        {
            son = await _e.QuizCevaplaAsync(await _e.QuizAsync(_ders1), dogru: false);
            Assert.False(son.GetProperty("gecti").GetBoolean());
        }

        Assert.True(son.GetProperty("videoSifirlandi").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await _e.PostAsync($"/api/v1/egitim/dersler/{_ders1}/quiz")).Durum);
        var ilerleme = await _f.VeriAsync(db => db.DersIlerlemeleri.SingleAsync(x => x.DersId == _ders1));
        Assert.Equal(0, ilerleme.IzlenenSaniye);
        Assert.Equal(1, ilerleme.Dongu);
    }

    [Fact]
    public async Task Baskasinin_quiz_denemesine_cevap_verilemez()
    {
        await _e.VideoyuIzleAsync(_ders1);
        var q = await _e.QuizAsync(_ders1);
        var baska = await EgitimIstemcisi.GirisAsync(_f, "musteri@ornek-spor.test");
        var (durum, _) = await baska.PostAsync($"/api/v1/egitim/quiz/{q.GetProperty("denemeId").GetGuid()}/cevap",
            new { cevaplar = new Dictionary<string, int>() });
        Assert.Equal(HttpStatusCode.NotFound, durum);
    }

    [Fact]
    public async Task Istek_anahtari_olmadan_egitim_uclari_calismaz()
        => Assert.Equal(HttpStatusCode.BadRequest, (await _e.PostAsync($"/api/v1/egitim/dersler/{_ders1}/oturum", csrf: false)).Durum);

    [Fact]
    public async Task Giris_yapmadan_egitim_uclari_401_doner()
    {
        var yanit = await _f.Istemci().PostAsync($"/api/v1/egitim/dersler/{_ders1}/oturum", null);
        Assert.Equal(HttpStatusCode.Unauthorized, yanit.StatusCode);
    }
}

[Collection(SqlKoleksiyonu.Ad)]
public sealed class SertifikaTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private UygulamaFabrikasi _f = null!;
    private EgitimIstemcisi _e = null!;
    private string _no = "";

    public async Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        _e = await EgitimIstemcisi.GirisAsync(_f);
        Assert.Null(await _e.DersiBitirAsync(1));
        Assert.Null(await _e.DersiBitirAsync(2));
        _no = (await _e.DersiBitirAsync(3))!;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public void Son_quiz_gecilince_tahmin_edilemez_numarali_sertifika_verilir()
    {
        Assert.Equal(26, _no.Length);
        Assert.Matches("^[A-Z2-7]{26}$", _no);
    }

    [Fact]
    public async Task Sertifika_PDF_i_sahibine_verilir()
    {
        var yanit = await _e.Http.GetAsync($"/egitim/sertifikalar/{_no}/pdf");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("application/pdf", yanit.Content.Headers.ContentType?.MediaType);
        var bayt = await yanit.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bayt, 0, 4));
        Assert.True(bayt.Length > 10_000);
    }

    [Fact]
    public async Task Baska_kullanici_sertifika_PDF_ini_indiremez()
    {
        var baska = await EgitimIstemcisi.GirisAsync(_f, "musteri@ornek-spor.test");
        Assert.Equal(HttpStatusCode.NotFound, (await baska.Http.GetAsync($"/egitim/sertifikalar/{_no}/pdf")).StatusCode);
    }

    [Fact]
    public async Task Herkese_acik_dogrulamada_tam_ad_gorunmez()
    {
        var anonim = _f.Istemci();
        var html = await anonim.GetStringAsync($"/sertifika/{_no}");
        Assert.Contains("data-sertifika=\"gecerli\"", html, StringComparison.Ordinal);
        Assert.Contains("A. Y.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ayşe", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Yılmaz", html, StringComparison.Ordinal);

        var api = await anonim.GetStringAsync($"/api/v1/sertifikalar/{_no.ToLowerInvariant()}");
        Assert.Contains("\"gecerli\":true", api, StringComparison.Ordinal);
        Assert.DoesNotContain("Ayşe", api, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bilinmeyen_numara_404_doner()
        => Assert.Equal(HttpStatusCode.NotFound, (await _f.Istemci().GetAsync("/api/v1/sertifikalar/AAAAAAAAAAAAAAAAAAAAAAAAAA")).StatusCode);

    [Fact]
    public async Task Iptal_silme_degil_harekettir_ve_gecmis_degistirilemez()
    {
        await using (var scope = _f.Services.CreateAsyncScope())
        {
            var s = scope.ServiceProvider.GetRequiredService<CihazServis.Application.Egitim.SertifikaServisi>();
            Assert.True((await s.IptalEtAsync(_no, 4, "Test iptali")).Basarili);
        }

        var html = await _f.Istemci().GetStringAsync($"/sertifika/{_no}");
        Assert.Contains("data-sertifika=\"iptal\"", html, StringComparison.Ordinal);
        Assert.Equal(2, await _f.VeriAsync(db => db.SertifikaHareketleri.CountAsync()));

        // Veritabanı tetikleyicisi geçmişin değiştirilmesini ve silinmesini engeller.
        await Assert.ThrowsAnyAsync<Exception>(() => _f.VeriAsync(db =>
            db.Database.ExecuteSqlRawAsync("UPDATE servis.SertifikaHareketleri SET Aciklama = 'degisti'")));
        await Assert.ThrowsAnyAsync<Exception>(() => _f.VeriAsync(db =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM servis.SertifikaHareketleri")));
    }
}
