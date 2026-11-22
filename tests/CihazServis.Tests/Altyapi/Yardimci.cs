using System.Text.RegularExpressions;

namespace CihazServis.Tests.Altyapi;

public static partial class Yardimci
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryDeseni();

    public static string AntiforgeryAnahtari(string html)
    {
        var m = AntiforgeryDeseni().Match(html);
        Assert.True(m.Success, "Sayfada antiforgery anahtarı yok.");
        return m.Groups[1].Value;
    }

    /// <summary>Demo kullanıcısıyla giriş yapar, çerez istemcide kalır.</summary>
    public static async Task GirisYap(HttpClient c, string eposta)
    {
        var sayfa = await c.GetStringAsync("/giris");
        var yanit = await c.PostAsync("/giris", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Eposta"] = eposta,
            ["Sifre"] = CihazServis.Infrastructure.Veri.DemoVeriTohumlayici.DemoSifre,
            ["__RequestVerificationToken"] = AntiforgeryAnahtari(sayfa),
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, yanit.StatusCode);
    }
}
