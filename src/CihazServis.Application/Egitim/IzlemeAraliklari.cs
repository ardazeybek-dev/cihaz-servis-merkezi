using System.Text.Json;

namespace CihazServis.Application.Egitim;

/// <summary>İzlenen video aralıklarını birleştirir ve toplam izlenen süreyi hesaplar.</summary>
public static class IzlemeAraliklari
{
    public static List<(double Bas, double Son)> Coz(string json)
    {
        var dizi = JsonSerializer.Deserialize<double[][]>(json) ?? [];
        return dizi.Where(a => a.Length == 2).Select(a => (a[0], a[1])).ToList();
    }

    public static string Yaz(IEnumerable<(double Bas, double Son)> araliklar)
        => JsonSerializer.Serialize(araliklar.Select(a => new[] { Math.Round(a.Bas, 2), Math.Round(a.Son, 2) }));

    /// <summary>Yeni aralığı ekler, çakışan ve 0,5 sn'den yakın aralıkları tek aralıkta birleştirir.</summary>
    public static List<(double Bas, double Son)> Ekle(IEnumerable<(double Bas, double Son)> mevcut, double bas, double son)
    {
        const double birlesmeAraligi = 0.5;
        var liste = mevcut.Append((bas, son)).Where(a => a.Item2 > a.Item1).OrderBy(a => a.Item1).ToList();
        var sonuc = new List<(double Bas, double Son)>();
        foreach (var a in liste)
        {
            if (sonuc.Count > 0 && a.Item1 <= sonuc[^1].Son + birlesmeAraligi)
            {
                sonuc[^1] = (sonuc[^1].Bas, Math.Max(sonuc[^1].Son, a.Item2));
            }
            else
            {
                sonuc.Add(a);
            }
        }

        return sonuc;
    }

    public static double Toplam(IEnumerable<(double Bas, double Son)> araliklar) => araliklar.Sum(a => a.Son - a.Bas);
}
