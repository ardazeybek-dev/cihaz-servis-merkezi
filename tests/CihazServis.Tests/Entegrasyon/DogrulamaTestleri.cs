using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CihazServis.Application.Varliklar;
using CihazServis.Tests.Altyapi;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Entegrasyon;

[Collection(SqlKoleksiyonu.Ad)]
public sealed class DogrulamaTestleri(SqlKonteyner sql) : IAsyncLifetime
{
    private UygulamaFabrikasi _f = null!;

    public Task InitializeAsync()
    {
        _f = new UygulamaFabrikasi(sql);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<JsonElement> Sorgula(string seri, string? tuzak = null, string? ip = null)
    {
        var yanit = await _f.Istemci(ip).PostAsJsonAsync("/api/v1/dogrulama", new { seriNo = seri, website = tuzak });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store", yanit.Headers.CacheControl?.ToString());
        return await yanit.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Kayitli_cihaz_bulunur_ve_garanti_devam_ediyor()
    {
        var j = await Sorgula("dmo a240-0001");
        Assert.Equal("bulundu", j.GetProperty("sonuc").GetString());
        Assert.Equal("DMO-A240-0001", j.GetProperty("seriNo").GetString());
        Assert.Equal("A100", j.GetProperty("model").GetProperty("kod").GetString());
        Assert.Equal("devamEdiyor", j.GetProperty("garanti").GetProperty("durum").GetString());
        Assert.Equal("2027-03-10", j.GetProperty("garanti").GetProperty("bitis").GetString());
        Assert.Equal("Plus", j.GetProperty("hizmetSeviyesi").GetString());
    }

    [Fact]
    public async Task Garantisi_bitmis_cihaz_bitti_doner()
    {
        var j = await Sorgula("DMO-A230-0002");
        Assert.Equal("bitti", j.GetProperty("garanti").GetProperty("durum").GetString());
        Assert.Equal(0, j.GetProperty("garanti").GetProperty("kalanGun").GetInt32());
    }

    [Fact]
    public async Task Kayitsiz_seri_bulunamadi_ve_servis_talebine_yonlendirir()
    {
        var j = await Sorgula("DMO-A240-9999");
        Assert.Equal("bulunamadi", j.GetProperty("sonuc").GetString());
        Assert.Equal("/servis/talep?seriNo=DMO-A240-9999", j.GetProperty("servisTalebiAdresi").GetString());
    }

    [Fact]
    public async Task Taninmayan_bicim_gecersizBicim_doner()
        => Assert.Equal("gecersizBicim", (await Sorgula("ABC")).GetProperty("sonuc").GetString());

    [Fact]
    public async Task Yanit_kurum_ya_da_kisi_bilgisi_icermez()
    {
        var yanit = await _f.Istemci().PostAsJsonAsync("/api/v1/dogrulama", new { seriNo = "DMO-A240-0001" });
        var ham = await yanit.Content.ReadAsStringAsync();
        var j = JsonDocument.Parse(ham).RootElement;

        // Yanıttaki alanlar tam olarak bunlar; yeni bir alan eklenirse test bilerek kırılır.
        Assert.Equal(
            ["garanti", "hizmetSeviyesi", "mesaj", "model", "seriNo", "servisTalebiAdresi", "sonuc"],
            j.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());

        var kurumlar = await _f.VeriAsync(db => db.Kurumlar.Select(k => k.Ad).ToListAsync());
        var kisiler = await _f.VeriAsync(db => db.Kullanicilar.Select(k => k.AdSoyad).ToListAsync());
        foreach (var gizli in kurumlar.Concat(kisiler).Append("kurulum").Append("kurum"))
        {
            Assert.DoesNotContain(gizli, ham, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Tuzak_alan_doluysa_gercek_sonuc_verilmez_ve_isaretlenir()
    {
        var j = await Sorgula("DMO-A240-0001", tuzak: "http://spam.example");
        Assert.Equal("bulunamadi", j.GetProperty("sonuc").GetString());
        Assert.True(await _f.VeriAsync(db => db.DogrulamaSorgulari.AnyAsync(s => s.TuzakDolu)));
    }

    [Fact]
    public async Task Sorgu_gunlugunde_acik_IP_tutulmaz()
    {
        await Sorgula("DMO-A240-0001", ip: "203.0.113.77");
        var kayit = await _f.VeriAsync(db => db.DogrulamaSorgulari.OrderByDescending(s => s.Id).FirstAsync());
        Assert.DoesNotContain("203.0.113", kayit.IpOzeti, StringComparison.Ordinal);
        Assert.Equal(32, kayit.IpOzeti.Length);
    }

    [Fact]
    public async Task Ayni_seri_numarasi_iki_kez_kaydedilemez()
    {
        var hata = await Assert.ThrowsAsync<DbUpdateException>(() => _f.VeriAsync(async db =>
        {
            var model = await db.CihazModelleri.FirstAsync();
            db.Cihazlar.Add(new Cihaz { SeriNo = "DMOA2400001", ModelId = model.Id, KurulumTarihi = new DateOnly(2026, 1, 1) });
            return await db.SaveChangesAsync();
        }));
        Assert.Contains("IX_Cihazlar_SeriNo", hata.InnerException?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Normalize_edilmemis_seri_numarasi_veritabanina_yazilamaz()
        => await Assert.ThrowsAsync<DbUpdateException>(() => _f.VeriAsync(async db =>
        {
            var model = await db.CihazModelleri.FirstAsync();
            db.Cihazlar.Add(new Cihaz { SeriNo = "dmo-a240-0100", ModelId = model.Id, KurulumTarihi = new DateOnly(2026, 1, 1) });
            return await db.SaveChangesAsync();
        }));

    [Fact]
    public async Task Tablolar_ayri_semada_durur()
    {
        var semalar = await _f.VeriAsync(db => db.Database
            .SqlQuery<string>($"SELECT DISTINCT TABLE_SCHEMA AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('Cihazlar','Kullanicilar','__EFMigrationsHistory')")
            .ToListAsync());
        Assert.Equal(["servis"], semalar);
    }

    [Fact]
    public async Task Garanti_sayfasi_formdan_sorgu_yapar()
    {
        var c = _f.Istemci();
        var sayfa = await c.GetStringAsync("/garanti");
        var anahtar = Yardimci.AntiforgeryAnahtari(sayfa);
        var yanit = await c.PostAsync("/garanti", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SeriNo"] = "DMO-B250-0003",
            ["__RequestVerificationToken"] = anahtar,
        }));
        var html = await yanit.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("data-sonuc=\"Bulundu\"", html, StringComparison.Ordinal);
        Assert.Contains("Demo Cihaz B200", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Bornova", html, StringComparison.Ordinal);
    }
}

[Collection(SqlKoleksiyonu.Ad)]
public sealed class HizSiniriTestleri(SqlKonteyner sql)
{
    [Fact]
    public async Task Sinir_asilinca_429_ve_Turkce_mesaj_doner_ve_diger_IP_etkilenmez()
    {
        await using var f = new UygulamaFabrikasi(sql, new Dictionary<string, string?> { ["HizSiniri:Dogrulama:Limit"] = "3" });
        var c = f.Istemci("198.51.100.10");
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/dogrulama", new { seriNo = "DMO-A240-0001" })).StatusCode);
        }

        var reddedilen = await c.PostAsJsonAsync("/api/v1/dogrulama", new { seriNo = "DMO-A240-0001" });
        Assert.Equal(HttpStatusCode.TooManyRequests, reddedilen.StatusCode);
        Assert.True(reddedilen.Headers.RetryAfter is not null);
        var j = await reddedilen.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cokFazlaIstek", j.GetProperty("hata").GetString());
        Assert.Contains("bekleyip", j.GetProperty("mesaj").GetString(), StringComparison.Ordinal);

        var baska = await f.Istemci("198.51.100.11").PostAsJsonAsync("/api/v1/dogrulama", new { seriNo = "DMO-A240-0001" });
        Assert.Equal(HttpStatusCode.OK, baska.StatusCode);
    }
}
