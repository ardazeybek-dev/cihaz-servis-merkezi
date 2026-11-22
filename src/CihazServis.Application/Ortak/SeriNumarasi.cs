using System.Text;

namespace CihazServis.Application.Ortak;

/// <summary>
/// Seri numarasını tek biçime indirger. Kullanıcı "dmo a240-0001" de yazsa,
/// etiketteki "DMO-A240-0001" de yazsa veritabanında aynı kayda düşer.
/// </summary>
public static class SeriNumarasi
{
    public const int AzamiUzunluk = 32;

    public static string Normalize(string? girdi)
    {
        if (string.IsNullOrWhiteSpace(girdi))
        {
            return "";
        }

        var sb = new StringBuilder(girdi.Length);
        foreach (var c in girdi)
        {
            if (char.IsWhiteSpace(c) || c is '-' or '_' or '.' or '/' or '–' or '—')
            {
                continue;
            }

            // Kültürden bağımsız büyük harf: tr-TR'de "i" -> "İ" olur ve eşleşme bozulur.
            sb.Append(char.ToUpperInvariant(c));
            if (sb.Length > AzamiUzunluk)
            {
                break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Etiketteki okunur biçim: DMOA2400001 -> DMO-A240-0001.</summary>
    public static string Goster(string normalize)
    {
        if (normalize.Length != 11)
        {
            return normalize;
        }

        return $"{normalize[..3]}-{normalize[3..7]}-{normalize[7..]}";
    }
}
