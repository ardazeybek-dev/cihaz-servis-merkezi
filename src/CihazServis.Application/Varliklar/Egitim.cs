namespace CihazServis.Application.Varliklar;

public class Kurs
{
    public int Id { get; set; }
    public string Ad { get; set; } = "";
    public string Aciklama { get; set; } = "";
    public int? ModelId { get; set; }
    public CihazModeli? Model { get; set; }
    public List<Ders> Dersler { get; set; } = [];
}

public class Ders
{
    public int Id { get; set; }
    public int KursId { get; set; }
    public Kurs Kurs { get; set; } = null!;

    /// <summary>1'den başlar. Bir ders, önceki dersin quiz'i geçilmeden açılmaz.</summary>
    public int Sira { get; set; }

    public string Baslik { get; set; } = "";
    public string Aciklama { get; set; } = "";

    /// <summary>Medya klasöründeki HLS paketinin adı (ör. ders-1).</summary>
    public string MedyaAnahtari { get; set; } = "";

    public double SureSaniye { get; set; }
    public List<QuizSorusu> Sorular { get; set; } = [];
}

public class QuizSorusu
{
    public int Id { get; set; }
    public int DersId { get; set; }
    public string Metin { get; set; } = "";
    public List<QuizSecenegi> Secenekler { get; set; } = [];
}

public class QuizSecenegi
{
    public int Id { get; set; }
    public int SoruId { get; set; }
    public string Metin { get; set; } = "";

    /// <summary>Yalnız sunucuda okunur, hiçbir yanıtla istemciye gönderilmez.</summary>
    public bool Dogru { get; set; }
}

/// <summary>Bir kullanıcının bir dersteki durumu.</summary>
public class DersIlerlemesi
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public int DersId { get; set; }
    public Ders Ders { get; set; } = null!;

    /// <summary>İzlenen aralıklar, birleştirilmiş hâlde JSON: [[0,10.2],[15,40]].</summary>
    public string IzlenenAraliklar { get; set; } = "[]";

    public double IzlenenSaniye { get; set; }
    public double EnIleriSaniye { get; set; }
    public DateTimeOffset? VideoTamamlandi { get; set; }
    public DateTimeOffset? QuizGecildi { get; set; }

    /// <summary>Quiz hakları biterse video sıfırlanır ve döngü bir artar.</summary>
    public int Dongu { get; set; }

    /// <summary>Aynı anda tek oynatma: yalnız bu oturumun bildirimleri kabul edilir.</summary>
    public Guid? AktifOturum { get; set; }

    public DateTimeOffset? OturumBaslangic { get; set; }

    /// <summary>Aktif oturumda kabul edilen toplam izleme süresi. Gerçek geçen süreyi aşamaz.</summary>
    public double OturumIzlenenSaniye { get; set; }

    public byte[] SurumDamgasi { get; set; } = [];
}

public class QuizDenemesi
{
    public Guid Id { get; set; }
    public int KullaniciId { get; set; }
    public int DersId { get; set; }
    public int Dongu { get; set; }
    public DateTimeOffset Baslangic { get; set; }
    public DateTimeOffset? Bitis { get; set; }

    /// <summary>Sorulan sorular ve seçeneklerin gösterilen sırası, JSON.</summary>
    public string SoruDizilimi { get; set; } = "[]";

    public int? DogruSayisi { get; set; }
    public int SoruSayisi { get; set; }
    public bool? Gecti { get; set; }
}

public class Sertifika
{
    public int Id { get; set; }

    /// <summary>128 bit rastgele değerden üretilir, sıralı değildir, tahmin edilemez.</summary>
    public string No { get; set; } = "";

    public int KullaniciId { get; set; }
    public int KursId { get; set; }
    public Kurs Kurs { get; set; } = null!;
    public string AdSoyad { get; set; } = "";
    public DateTimeOffset VerilisZamani { get; set; }
    public List<SertifikaHareketi> Hareketler { get; set; } = [];
}

public enum SertifikaHareketTuru
{
    Verildi = 1,
    IptalEdildi = 2,
}

/// <summary>Yalnız eklenir, silinmez ve değiştirilmez. Sertifikanın geçerliliği son hareketten okunur.</summary>
public class SertifikaHareketi
{
    public long Id { get; set; }
    public int SertifikaId { get; set; }
    public SertifikaHareketTuru Tur { get; set; }
    public DateTimeOffset Zaman { get; set; }
    public string? Aciklama { get; set; }
    public int? YapanKullaniciId { get; set; }
}
