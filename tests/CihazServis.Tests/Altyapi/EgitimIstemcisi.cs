using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Tests.Altyapi;

/// <summary>Bir demo kullanıcısı adına eğitim uçlarını çağıran test yardımcısı.</summary>
public sealed partial class EgitimIstemcisi
{
    private readonly UygulamaFabrikasi _f;
    private string _csrf = "";

    public HttpClient Http { get; }

    private EgitimIstemcisi(UygulamaFabrikasi f, HttpClient c)
    {
        _f = f;
        Http = c;
    }

    [GeneratedRegex("data-csrf=\"([^\"]+)\"")]
    private static partial Regex CsrfDeseni();

    public static async Task<EgitimIstemcisi> GirisAsync(UygulamaFabrikasi f, string eposta = "musteri@ornek-klinik.test")
    {
        var c = f.Istemci();
        await Yardimci.GirisYap(c, eposta);
        var e = new EgitimIstemcisi(f, c);
        var ilk = await e.DersIdAsync(1);
        e._csrf = CsrfDeseni().Match(await c.GetStringAsync($"/egitim/ders/{ilk}")).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(e._csrf));
        return e;
    }

    public Task<int> DersIdAsync(int sira) => _f.VeriAsync(db => db.Dersler.Where(d => d.Sira == sira).Select(d => d.Id).FirstAsync());

    public async Task<(HttpStatusCode Durum, JsonElement Govde)> PostAsync(string yol, object? govde = null, bool csrf = true)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Post, yol) { Content = JsonContent.Create(govde ?? new { }) };
        if (csrf)
        {
            istek.Headers.Add("X-CSRF-TOKEN", _csrf);
        }

        var yanit = await Http.SendAsync(istek);
        var metin = await yanit.Content.ReadAsStringAsync();
        return (yanit.StatusCode, metin.Length > 0 ? JsonDocument.Parse(metin).RootElement.Clone() : default);
    }

    public async Task<JsonElement> OturumAsync(int dersId)
    {
        var (durum, j) = await PostAsync($"/api/v1/egitim/dersler/{dersId}/oturum");
        Assert.Equal(HttpStatusCode.OK, durum);
        return j;
    }

    public Task<(HttpStatusCode Durum, JsonElement Govde)> IzlemeAsync(int dersId, Guid oturum, double bas, double son)
        => PostAsync($"/api/v1/egitim/dersler/{dersId}/izleme", new { oturumId = oturum, baslangic = bas, bitis = son });

    /// <summary>Videoyu gerçek hızda izliyormuş gibi 10 sn'lik parçalarla bildirir.</summary>
    public async Task VideoyuIzleAsync(int dersId, double sure = 40)
    {
        var oturum = (await OturumAsync(dersId)).GetProperty("oturumId").GetGuid();
        for (double t = 0; t < sure; t += 10)
        {
            _f.Saat.Advance(TimeSpan.FromSeconds(10));
            var (durum, _) = await IzlemeAsync(dersId, oturum, t, Math.Min(t + 10, sure));
            Assert.Equal(HttpStatusCode.OK, durum);
        }
    }

    public async Task<JsonElement> QuizAsync(int dersId)
    {
        var (durum, j) = await PostAsync($"/api/v1/egitim/dersler/{dersId}/quiz");
        Assert.Equal(HttpStatusCode.OK, durum);
        return j;
    }

    /// <summary>Doğru (ya da bilerek yanlış) cevaplarla quiz'i gönderir. Doğrular test tarafında veritabanından okunur.</summary>
    public async Task<JsonElement> QuizCevaplaAsync(JsonElement quiz, bool dogru)
    {
        var soruIdleri = quiz.GetProperty("sorular").EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToList();
        var secenekler = await _f.VeriAsync(db => db.QuizSecenekleri.Where(s => soruIdleri.Contains(s.SoruId)).ToListAsync());
        var cevaplar = soruIdleri.ToDictionary(
            id => id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            id => secenekler.First(s => s.SoruId == id && s.Dogru == dogru).Id);
        var (durum, j) = await PostAsync($"/api/v1/egitim/quiz/{quiz.GetProperty("denemeId").GetGuid()}/cevap", new { cevaplar });
        Assert.Equal(HttpStatusCode.OK, durum);
        return j;
    }

    public async Task<string?> DersiBitirAsync(int sira)
    {
        var id = await DersIdAsync(sira);
        await VideoyuIzleAsync(id);
        var sonuc = await QuizCevaplaAsync(await QuizAsync(id), dogru: true);
        Assert.True(sonuc.GetProperty("gecti").GetBoolean());
        return sonuc.GetProperty("sertifikaNo").GetString();
    }
}
