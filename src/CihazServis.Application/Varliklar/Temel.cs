namespace CihazServis.Application.Varliklar;

public class Kurum
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
}

public enum KullaniciRolu
{
    Musteri = 1,
    Personel = 2,
    Yonetici = 3,
}

public class Kullanici
{
    public int Id { get; set; }
    public string Eposta { get; set; } = "";
    public string AdSoyad { get; set; } = "";
    public string SifreOzeti { get; set; } = "";
    public KullaniciRolu Rol { get; set; }
    public int? KurumId { get; set; }
    public Kurum? Kurum { get; set; }
}

public class CihazModeli
{
    public int Id { get; set; }

    /// <summary>Kısa model kodu, ör. A100.</summary>
    public string Kod { get; set; } = "";

    public string Ad { get; set; } = "";

    /// <summary>Normalize edilmiş seri numarasının uyması gereken düzenli ifade.</summary>
    public string SeriNoDeseni { get; set; } = "";

    /// <summary>Cihaz kaydında garanti bitişi boşsa kurulum tarihine eklenen süre.</summary>
    public int VarsayilanGarantiAy { get; set; }
}

public enum HizmetSeviyesi
{
    Standart = 1,
    Plus = 2,
}

public class Cihaz
{
    public int Id { get; set; }

    /// <summary>Normalize edilmiş seri numarası (tekil indeksli).</summary>
    public string SeriNo { get; set; } = "";

    public int ModelId { get; set; }
    public CihazModeli Model { get; set; } = null!;
    public int? KurumId { get; set; }
    public Kurum? Kurum { get; set; }
    public DateOnly KurulumTarihi { get; set; }

    /// <summary>Boşsa modelin varsayılan garanti süresinden hesaplanır.</summary>
    public DateOnly? GarantiBitis { get; set; }

    public HizmetSeviyesi HizmetSeviyesi { get; set; } = HizmetSeviyesi.Standart;
}
