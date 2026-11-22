namespace CihazServis.Application.Varliklar;

public enum TalepKonusu
{
    Ariza = 1,
    Bakim = 2,
    Kurulum = 3,
    Diger = 4,
}

public enum TalepDurumu
{
    Alindi = 1,
    Inceleniyor = 2,
    ParcaBekleniyor = 3,
    Serviste = 4,
    Tamamlandi = 5,
    Iptal = 6,
}

public class ServisTalebi
{
    public int Id { get; set; }

    /// <summary>SR-2026-000001 biçiminde, yıl içinde sıralı ve benzersiz.</summary>
    public string No { get; set; } = "";

    /// <summary>Formda yazılan seri numarasının normalize hâli. Kayıtlı olmasa da talep alınır.</summary>
    public string SeriNo { get; set; } = "";

    public int? CihazId { get; set; }
    public Cihaz? Cihaz { get; set; }

    /// <summary>Seri numarası kayıtlı bir cihazla eşleşti mi.</summary>
    public bool Dogrulandi { get; set; }

    public TalepKonusu Konu { get; set; }
    public string Aciklama { get; set; } = "";

    public string AdSoyad { get; set; } = "";
    public string? KurumAdi { get; set; }

    /// <summary>Yalnız rakamlar, 10 hane (5XXXXXXXXX ya da sabit hat).</summary>
    public string Telefon { get; set; } = "";

    public string Eposta { get; set; } = "";

    /// <summary>Formda onaylanan aydınlatma metninin sürümü ve onay zamanı.</summary>
    public string AydinlatmaSurumu { get; set; } = "";

    public DateTimeOffset AydinlatmaOnayZamani { get; set; }

    public TalepDurumu Durum { get; set; } = TalepDurumu.Alindi;
    public DateTimeOffset OlusturmaZamani { get; set; }

    /// <summary>Konuya göre iş günü takviminden hesaplanan hedef tamamlanma tarihi.</summary>
    public DateOnly HedefTarih { get; set; }

    public int SorumluId { get; set; }
    public Kullanici Sorumlu { get; set; } = null!;

    public string IpOzeti { get; set; } = "";

    public byte[] SurumDamgasi { get; set; } = [];

    public List<TalepEki> Ekler { get; set; } = [];
    public List<TalepHareketi> Hareketler { get; set; } = [];
}

public class TalepEki
{
    public int Id { get; set; }
    public int TalepId { get; set; }

    /// <summary>Kullanıcının yüklediği ad, yalnız gösterim ve indirme adı için.</summary>
    public string OrijinalAd { get; set; } = "";

    /// <summary>Depodaki rastgele ad. Kullanıcı girdisinden türetilmez.</summary>
    public string DepoAdi { get; set; } = "";

    /// <summary>Dosya imzasından tespit edilen içerik türü.</summary>
    public string IcerikTuru { get; set; } = "";

    public long Boyut { get; set; }
}

/// <summary>Yalnız eklenir. Talebin zaman çizelgesi bu tablodan okunur.</summary>
public class TalepHareketi
{
    public long Id { get; set; }
    public int TalepId { get; set; }
    public TalepDurumu? EskiDurum { get; set; }
    public TalepDurumu YeniDurum { get; set; }
    public DateTimeOffset Zaman { get; set; }

    /// <summary>Durum sorgusunda ve e-postada müşteriye görünür.</summary>
    public string? MusteriNotu { get; set; }

    /// <summary>Yalnız yönetim ekranında görünür, hiçbir dış yanıta girmez.</summary>
    public string? IcNot { get; set; }

    public int? YapanKullaniciId { get; set; }
    public Kullanici? YapanKullanici { get; set; }
}

/// <summary>Yeni talebin kime düşeceği. Öncelik küçük olan önce denenir; hiçbiri uymazsa varsayılan yönetici.</summary>
public class AtamaKurali
{
    public int Id { get; set; }
    public int Oncelik { get; set; }
    public TalepKonusu? Konu { get; set; }
    public int? ModelId { get; set; }
    public CihazModeli? Model { get; set; }
    public int SorumluId { get; set; }
    public Kullanici Sorumlu { get; set; } = null!;
    public bool Etkin { get; set; } = true;
}

/// <summary>Talep numarası sayacı, yıl başına bir satır.</summary>
public class TalepSayaci
{
    public int Yil { get; set; }
    public int Son { get; set; }
}

/// <summary>Durum sorgusunda talep numarası başına hatalı deneme sayacı. Anahtar normalize edilmiş numaradır.</summary>
public class DurumSorguKilidi
{
    public string TalepNo { get; set; } = "";
    public int HataliDeneme { get; set; }
    public DateTimeOffset SonHata { get; set; }
    public DateTimeOffset? KilitBitis { get; set; }
}

/// <summary>Yalnız eklenir (tetikleyiciyle korunur). Kim, ne zaman, neye ne yaptı.</summary>
public class DenetimKaydi
{
    public long Id { get; set; }
    public DateTimeOffset Zaman { get; set; }
    public int? KullaniciId { get; set; }
    public string Islem { get; set; } = "";
    public string Varlik { get; set; } = "";
    public string? VarlikNo { get; set; }
    public string? Ayrinti { get; set; }
}
