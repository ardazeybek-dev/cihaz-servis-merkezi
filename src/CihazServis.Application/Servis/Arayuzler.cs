namespace CihazServis.Application.Servis;

/// <summary>Yıl içinde sıralı talep numarası. Eşzamanlı isteklerde aynı numarayı iki kez vermez.</summary>
public interface ITalepNoUretici
{
    Task<string> SonrakiAsync(int yil, CancellationToken ct = default);
}

/// <summary>Talep ekleri web kökü dışında, rastgele adla saklanır.</summary>
public interface IDosyaDeposu
{
    Task<string> KaydetAsync(Stream icerik, CancellationToken ct = default);

    Stream? Ac(string depoAdi);

    void Sil(string depoAdi);
}

/// <summary>E-postalar istek içinde değil arka planda gönderilir. Kuyruğa yalnız kimlik verilir.</summary>
public interface IBildirimKuyrugu
{
    void TalepAlindi(int talepId);

    void DurumDegisti(long hareketId);
}

public sealed record EpostaMesaji(string AliciEposta, string AliciAd, string Konu, string Metin);

public interface IEpostaGonderici
{
    Task GonderAsync(EpostaMesaji mesaj, CancellationToken ct = default);
}
