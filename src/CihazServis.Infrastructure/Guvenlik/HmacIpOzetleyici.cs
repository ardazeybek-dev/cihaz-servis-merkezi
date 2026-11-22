using System.Security.Cryptography;
using System.Text;
using CihazServis.Application.Ortak;
using Microsoft.Extensions.Configuration;

namespace CihazServis.Infrastructure.Guvenlik;

public sealed class HmacIpOzetleyici(IConfiguration config) : IIpOzetleyici
{
    private readonly byte[] _anahtar = Encoding.UTF8.GetBytes(
        config["Guvenlik:IpOzetAnahtari"] is { Length: >= 16 } a
            ? a
            : throw new InvalidOperationException("Guvenlik:IpOzetAnahtari en az 16 karakter olmalı."));

    public string Ozetle(string? ip)
    {
        var ozet = HMACSHA256.HashData(_anahtar, Encoding.UTF8.GetBytes(ip ?? "bilinmiyor"));
        return Convert.ToHexStringLower(ozet.AsSpan(0, 16));
    }
}
