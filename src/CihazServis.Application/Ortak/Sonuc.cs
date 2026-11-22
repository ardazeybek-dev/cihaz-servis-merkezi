namespace CihazServis.Application.Ortak;

public enum HataTuru
{
    Yok,
    Bulunamadi,
    Yasak,
    Cakisma,
    Gecersiz,
}

/// <summary>Servis sonucu: başarılıysa değer, değilse hata türü ve kullanıcıya gösterilecek mesaj.</summary>
public sealed record Sonuc<T>(T? Deger, HataTuru Hata, string? Kod, string? Mesaj)
{
    public bool Basarili => Hata == HataTuru.Yok;

    public static Sonuc<T> Tamam(T deger) => new(deger, HataTuru.Yok, null, null);

    public static Sonuc<T> Bulunamadi(string mesaj = "Kayıt bulunamadı.") => new(default, HataTuru.Bulunamadi, "bulunamadi", mesaj);

    public static Sonuc<T> Yasak(string kod, string mesaj) => new(default, HataTuru.Yasak, kod, mesaj);

    public static Sonuc<T> Cakisma(string kod, string mesaj) => new(default, HataTuru.Cakisma, kod, mesaj);

    public static Sonuc<T> Gecersiz(string kod, string mesaj) => new(default, HataTuru.Gecersiz, kod, mesaj);
}
