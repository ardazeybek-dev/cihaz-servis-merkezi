using System.Net.Http.Headers;

namespace CihazServis.Tests.Altyapi;

public sealed record TestEki(string Ad, byte[] Icerik);

public static class TalepIstemcisi
{
    public static Dictionary<string, string> GecerliAlanlar(string? seri = "DMO-A240-0001", string konu = "Ariza") => new()
    {
        ["SeriNo"] = seri ?? "",
        ["Konu"] = konu,
        ["Aciklama"] = "Ekranda E-21 hata kodu çıkıyor, ölçüm yarıda kalıyor.",
        ["AdSoyad"] = "Deneme Kullanıcı",
        ["KurumAdi"] = "Deneme Kurumu",
        ["Telefon"] = "0555 111 22 33",
        ["Eposta"] = "deneme@ornek.test",
        ["AydinlatmaOnay"] = "true",
        ["AydinlatmaSurumu"] = CihazServis.Application.Servis.AydinlatmaMetni.Surum,
    };

    /// <summary>Formu tarayıcı gibi gönderir: önce sayfadan antiforgery anahtarını alır.</summary>
    public static async Task<HttpResponseMessage> GonderAsync(HttpClient c, Dictionary<string, string> alanlar, params TestEki[] ekler)
    {
        var sayfa = await c.GetStringAsync("/servis/talep");
        using var icerik = new MultipartFormDataContent();
        foreach (var (k, v) in alanlar)
        {
            icerik.Add(new StringContent(v), k);
        }

        icerik.Add(new StringContent(Yardimci.AntiforgeryAnahtari(sayfa)), "__RequestVerificationToken");
        foreach (var e in ekler)
        {
            var dosya = new ByteArrayContent(e.Icerik);
            dosya.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            icerik.Add(dosya, "Ekler", e.Ad);
        }

        return await c.PostAsync("/servis/talep", icerik);
    }
}
