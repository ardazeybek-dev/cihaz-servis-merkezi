namespace CihazServis.Application.Ortak;

/// <summary>
/// Video dosyaları için süreli ve kullanıcıya bağlı imza üretir. İmzasız ya da süresi
/// geçmiş bağlantı açılmaz, başka bir kullanıcının bağlantısı da işe yaramaz.
/// </summary>
public interface IMedyaImzalayici
{
    /// <summary>Medya adresine eklenecek sorgu dizesini döner (başında ? olmadan).</summary>
    string Imzala(string medyaAnahtari, int kullaniciId);

    bool Dogrula(string medyaAnahtari, int kullaniciId, long bitisUnix, string imza);
}
