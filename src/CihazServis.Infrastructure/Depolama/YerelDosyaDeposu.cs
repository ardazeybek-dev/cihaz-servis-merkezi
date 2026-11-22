using CihazServis.Application.Servis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CihazServis.Infrastructure.Depolama;

/// <summary>
/// Ekleri web kökü dışındaki klasöre rastgele adla yazar. Ad kullanıcıdan gelmediği için yol
/// atlatma mümkün değildir; yine de okurken adın biçimi denetlenir.
/// </summary>
public sealed class YerelDosyaDeposu : IDosyaDeposu
{
    private readonly string _kok;

    public YerelDosyaDeposu(IConfiguration config, IHostEnvironment env)
    {
        _kok = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Depolama:Klasor"] is { Length: > 0 } k ? k : "storage"));
    }

    public async Task<string> KaydetAsync(Stream icerik, CancellationToken ct = default)
    {
        var ad = Guid.NewGuid().ToString("N");
        var yol = Yol(ad);
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        await using var hedef = new FileStream(yol, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await icerik.CopyToAsync(hedef, ct);
        return ad;
    }

    public Stream? Ac(string depoAdi)
    {
        if (!GecerliAd(depoAdi))
        {
            return null;
        }

        var yol = Yol(depoAdi);
        return File.Exists(yol) ? new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    public void Sil(string depoAdi)
    {
        if (GecerliAd(depoAdi))
        {
            File.Delete(Yol(depoAdi));
        }
    }

    private static bool GecerliAd(string ad) => ad.Length == 32 && ad.All(Uri.IsHexDigit);

    /// <summary>İlk iki karakter alt klasör olur, tek klasörde on binlerce dosya birikmez.</summary>
    private string Yol(string ad) => Path.Combine(_kok, "ekler", ad[..2], ad);
}
