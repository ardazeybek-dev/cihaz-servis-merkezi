using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CihazServis.Application.Ortak;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace CihazServis.Infrastructure.Guvenlik;

/// <summary>
/// Süreli, kullanıcıya bağlı HMAC-SHA256 imza. Canlıda video bir CDN'den (ör. Bunny Stream)
/// verilecekse aynı mantık CDN'in belirteçli bağlantı özelliğiyle kurulur.
/// </summary>
public sealed class HmacMedyaImzalayici(IConfiguration config, TimeProvider saat) : IMedyaImzalayici
{
    private readonly byte[] _anahtar = Encoding.UTF8.GetBytes(
        config["Guvenlik:MedyaImzaAnahtari"] is { Length: >= 16 } a
            ? a
            : throw new InvalidOperationException("Guvenlik:MedyaImzaAnahtari en az 16 karakter olmalı."));

    private readonly TimeSpan _sure = TimeSpan.FromMinutes(config.GetValue("Medya:ImzaSuresiDakika", 5));

    public string Imzala(string medyaAnahtari, int kullaniciId)
    {
        var bitis = saat.GetUtcNow().Add(_sure).ToUnixTimeSeconds();
        return string.Create(CultureInfo.InvariantCulture, $"u={kullaniciId}&e={bitis}&s={Hesapla(medyaAnahtari, kullaniciId, bitis)}");
    }

    public bool Dogrula(string medyaAnahtari, int kullaniciId, long bitisUnix, string imza)
    {
        if (bitisUnix < saat.GetUtcNow().ToUnixTimeSeconds())
        {
            return false;
        }

        var beklenen = Encoding.ASCII.GetBytes(Hesapla(medyaAnahtari, kullaniciId, bitisUnix));
        var gelen = Encoding.ASCII.GetBytes(imza);
        return beklenen.Length == gelen.Length && CryptographicOperations.FixedTimeEquals(beklenen, gelen);
    }

    private string Hesapla(string medyaAnahtari, int kullaniciId, long bitis)
    {
        var veri = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{medyaAnahtari}|{kullaniciId}|{bitis}"));
        return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_anahtar, veri));
    }
}
