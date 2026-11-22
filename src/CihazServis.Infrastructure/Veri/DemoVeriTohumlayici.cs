using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Infrastructure.Veri;

/// <summary>
/// Yalnız demo ve geliştirme ortamı için uydurma örnek veri. Gerçek bir firmaya,
/// kişiye ya da cihaza ait değildir.
/// </summary>
public sealed class DemoVeriTohumlayici(UygulamaDbContext db, IPasswordHasher<Kullanici> hasher)
{
    public const string DemoSifre = "Demo.2026!";

    public const double DemoVideoSaniye = 40;

    public async Task TohumlaAsync(CancellationToken ct = default)
    {
        if (!await db.Kurumlar.AnyAsync(ct))
        {
            await TemelVeriAsync(ct);
        }

        if (!await db.Kurslar.AnyAsync(ct))
        {
            await KursAsync(ct);
        }

        if (!await db.AtamaKurallari.AnyAsync(ct))
        {
            await ServisAsync(ct);
        }
    }

    /// <summary>Demo telefonları uydurmadır; durum sorgusunda son 4 hane 1234 ve 5678.</summary>
    public const string DemoTalepAcik = "SR-2026-000001";
    public const string DemoTalepKapali = "SR-2026-000002";

    private async Task ServisAsync(CancellationToken ct)
    {
        var personel = await db.Kullanicilar.FirstAsync(k => k.Eposta == "personel@servis.test", ct);
        var yonetici = await db.Kullanicilar.FirstAsync(k => k.Eposta == "yonetici@servis.test", ct);
        var a100 = await db.CihazModelleri.FirstAsync(m => m.Kod == "A100", ct);
        db.AtamaKurallari.AddRange(
            new AtamaKurali { Oncelik = 10, Konu = TalepKonusu.Kurulum, Sorumlu = personel },
            new AtamaKurali { Oncelik = 20, Konu = TalepKonusu.Ariza, Model = a100, Sorumlu = personel });

        var c1 = await db.Cihazlar.FirstAsync(c => c.SeriNo == "DMOA2400001", ct);
        var c3 = await db.Cihazlar.FirstAsync(c => c.SeriNo == "DMOB2500003", ct);
        DateTimeOffset Z(int gun, int saat) => new(2026, 11, gun, saat, 0, 0, TimeSpan.Zero);

        var acik = Talep(DemoTalepAcik, c1, TalepKonusu.Ariza, "Ölçüm sırasında ekranda E-21 hata kodu çıkıyor, ölçüm yarıda kalıyor.",
            "Ayşe Yılmaz", "Örnek Klinik Kadıköy", "5550001234", "musteri@ornek-klinik.test", Z(16, 7), new DateOnly(2026, 11, 18), personel);
        acik.Hareketler.Add(H(TalepDurumu.Alindi, TalepDurumu.Inceleniyor, Z(16, 9), "Talebiniz incelemeye alındı.", "E-21 elektrot temas hatası olabilir.", personel));
        acik.Hareketler.Add(H(TalepDurumu.Inceleniyor, TalepDurumu.ParcaBekleniyor, Z(17, 8), "Değişecek elektrot plakası sipariş edildi.", "Tedarikçi teslim 2 gün dedi.", personel));
        acik.Hareketler.Add(H(TalepDurumu.ParcaBekleniyor, TalepDurumu.Serviste, Z(19, 11), "Parça geldi, servis ziyareti planlandı.", null, personel));
        acik.Durum = TalepDurumu.Serviste;

        var kapali = Talep(DemoTalepKapali, c3, TalepKonusu.Bakim, "Yıllık periyodik bakım talebi.",
            "Mehmet Demir", "Örnek Spor Salonu Bornova", "5550005678", "musteri@ornek-spor.test", Z(10, 6), new DateOnly(2026, 11, 16), yonetici);
        kapali.Hareketler.Add(H(TalepDurumu.Alindi, TalepDurumu.Inceleniyor, Z(10, 10), null, null, yonetici));
        kapali.Hareketler.Add(H(TalepDurumu.Inceleniyor, TalepDurumu.Serviste, Z(12, 7), "Bakım ziyareti yapılıyor.", null, yonetici));
        kapali.Hareketler.Add(H(TalepDurumu.Serviste, TalepDurumu.Tamamlandi, Z(12, 13), "Bakım tamamlandı, kalibrasyon kontrol edildi.", "Bir sonraki bakım Kasım 2027.", yonetici));
        kapali.Durum = TalepDurumu.Tamamlandi;

        db.ServisTalepleri.AddRange(acik, kapali);
        db.TalepSayaclari.Add(new TalepSayaci { Yil = 2026, Son = 2 });
        await db.SaveChangesAsync(ct);
    }

    private static ServisTalebi Talep(string no, Cihaz cihaz, TalepKonusu konu, string aciklama, string ad, string kurum, string tel, string eposta,
        DateTimeOffset zaman, DateOnly hedef, Kullanici sorumlu)
    {
        var t = new ServisTalebi
        {
            No = no, SeriNo = cihaz.SeriNo, Cihaz = cihaz, Dogrulandi = true, Konu = konu, Aciklama = aciklama, AdSoyad = ad, KurumAdi = kurum,
            Telefon = tel, Eposta = eposta, AydinlatmaSurumu = Application.Servis.AydinlatmaMetni.Surum, AydinlatmaOnayZamani = zaman,
            OlusturmaZamani = zaman, HedefTarih = hedef, Sorumlu = sorumlu, IpOzeti = "demo",
        };
        t.Hareketler.Add(new TalepHareketi { YeniDurum = TalepDurumu.Alindi, Zaman = zaman });
        return t;
    }

    private static TalepHareketi H(TalepDurumu eski, TalepDurumu yeni, DateTimeOffset zaman, string? musteri, string? ic, Kullanici yapan)
        => new() { EskiDurum = eski, YeniDurum = yeni, Zaman = zaman, MusteriNotu = musteri, IcNot = ic, YapanKullanici = yapan };

    private async Task KursAsync(CancellationToken ct)
    {
        var a100 = await db.CihazModelleri.FirstAsync(m => m.Kod == "A100", ct);
        var kurs = new Kurs
        {
            Ad = "Demo Cihaz A100 kullanım eğitimi",
            Aciklama = "Cihazı kuracak ve her gün kullanacak personel için üç kısa ders. Her dersin sonunda beş soru var, geçme notu 80.",
            Model = a100,
        };
        kurs.Dersler.Add(Ders(1, "Cihazı tanıyın", "Kurulum yeri, seri numarası etiketi ve ilk açılış.",
            S("Cihazı ilk kez açmadan önce ne yapılmalı?", "Güç kablosunun topraklı bir prize takılı olduğu kontrol edilir", "Cihaz ıslak bezle silinir", "Seri numarası etiketi sökülür"),
            S("Cihazın seri numarası nerede yazar?", "Arka yüzdeki etikette", "Açılış ekranındaki saatin altında", "Yalnız faturada"),
            S("Cihaz nasıl bir zemine kurulmalı?", "Düz ve sarsıntısız bir zemine", "Kalın bir halının üzerine", "Eğimli bir rampaya"),
            S("Cihaz doğrudan güneş alan bir pencerenin önüne konabilir mi?", "Hayır, ısı ve ışık ölçümü etkileyebilir", "Evet, fark etmez", "Yalnız kış aylarında konabilir"),
            S("Kurulumdan sonra garanti durumunu nereden görebilirsiniz?", "Garanti sorgulama sayfasından seri numarasıyla", "Cihazın güç düğmesine uzun basarak", "Yalnız satıcıyı arayarak"),
            S("Havalandırma açıkları için hangisi doğru?", "Önleri kapatılmaz, duvarla arada boşluk bırakılır", "Toz girmesin diye bantlanır", "Üzerleri havluyla örtülür")));
        kurs.Dersler.Add(Ders(2, "Günlük kullanım", "Ölçüm öncesi hazırlık, ölçüm sırasında dikkat edilecekler, sonuçların paylaşımı.",
            S("Ölçüme başlamadan önce kullanıcıdan ne istenir?", "Ayakkabı ve çoraplarını çıkarması", "Cihazın üzerinde zıplaması", "Telefonunu cihaza dayaması"),
            S("Ölçüm sırasında kullanıcı nasıl durmalı?", "Hareketsiz ve elektrotlara tam temas ederek", "Konuşarak ve yürüyerek", "Tek ayağının üzerinde"),
            S("Ölçüm sonucu beklenmedik görünüyorsa ilk adım nedir?", "Ölçümü uygun koşullarda tekrarlamak", "Cihazı bir hafta kapalı tutmak", "Sonucu elle düzeltmek"),
            S("Ekranda bir hata kodu görürseniz ne yaparsınız?", "Kodu not alıp servis talebi açarsınız", "Cihazın kapağını açıp içine bakarsınız", "Görmezden gelip devam edersiniz"),
            S("Kişisel ölçüm sonuçları için hangisi doğru?", "Yalnız ilgili kişiyle ve yetkili personelle paylaşılır", "Bekleme salonundaki ekranda gösterilir", "Herkese açık panoya asılır"),
            S("Gün sonunda cihaz için önerilen işlem hangisi?", "Ekranı kapatıp yüzeyleri kuru bezle silmek", "Fişi takılıyken üzerini ıslak bezle silmek", "Üzerine ağır eşya koymak")));
        kurs.Dersler.Add(Ders(3, "Temizlik ve bakım", "Elektrot temizliği, sıvı teması, kablo kontrolü ve periyodik bakım.",
            S("Elektrot yüzeyleri neyle temizlenir?", "Üreticinin önerdiği, alkol içermeyen yumuşak bezle", "Aşındırıcı süngerle", "Çamaşır suyuyla"),
            S("Temizlikten önce ne yapılmalı?", "Cihaz kapatılıp fişi çekilir", "Cihaz ölçüm modunda bırakılır", "Ekran parlaklığı artırılır"),
            S("Cihazın üzerine sıvı dökülürse ne yapılır?", "Cihaz kapatılır, kurulanır ve servis talebi açılır", "Saç kurutma makinesiyle ısıtılır", "Kullanmaya devam edilir"),
            S("Kabloda ezilme ya da kesik görürseniz ne yaparsınız?", "Cihazı kullanmayı bırakıp servis talebi açarsınız", "Kabloyu bantla sararsınız", "Kabloyu kendiniz değiştirirsiniz"),
            S("Yazılım güncellemesi bildirimi gelirse ne yapılır?", "Ölçüm yapılmayan bir saatte, cihaz kapatılmadan tamamlanır", "Güncelleme sürerken fiş çekilir", "Bildirim kalıcı olarak kapatılır"),
            S("Periyodik bakım zamanı geldiğinde ne yapılır?", "Servis talebi formundan bakım istenir", "Cihaz sökülüp içi temizlenir", "Bakım bir sonraki yıla ertelenir")));
        db.Kurslar.Add(kurs);
        await db.SaveChangesAsync(ct);
    }

    private static Ders Ders(int sira, string baslik, string aciklama, params QuizSorusu[] sorular)
    {
        var d = new Ders { Sira = sira, Baslik = baslik, Aciklama = aciklama, MedyaAnahtari = $"ders-{sira}", SureSaniye = DemoVideoSaniye };
        d.Sorular.AddRange(sorular);
        return d;
    }

    /// <summary>İlk seçenek doğru cevaptır; sorular gösterilirken seçeneklerin sırası karıştırılır.</summary>
    private static QuizSorusu S(string metin, string dogru, params string[] yanlislar)
    {
        var s = new QuizSorusu { Metin = metin };
        s.Secenekler.Add(new QuizSecenegi { Metin = dogru, Dogru = true });
        s.Secenekler.AddRange(yanlislar.Select(y => new QuizSecenegi { Metin = y }));
        return s;
    }

    private async Task TemelVeriAsync(CancellationToken ct)
    {

        var klinik = new Kurum { Ad = "Örnek Klinik Kadıköy" };
        var spor = new Kurum { Ad = "Örnek Spor Salonu Bornova" };
        db.Kurumlar.AddRange(klinik, spor);

        var a100 = new CihazModeli { Kod = "A100", Ad = "Demo Cihaz A100", SeriNoDeseni = "^DMOA[0-9]{7}$", VarsayilanGarantiAy = 24 };
        var b200 = new CihazModeli { Kod = "B200", Ad = "Demo Cihaz B200", SeriNoDeseni = "^DMOB[0-9]{7}$", VarsayilanGarantiAy = 24 };
        var c300 = new CihazModeli { Kod = "C300", Ad = "Demo Cihaz C300", SeriNoDeseni = "^DMOC[0-9]{7}$", VarsayilanGarantiAy = 12 };
        db.CihazModelleri.AddRange(a100, b200, c300);

        db.Cihazlar.AddRange(
            Cihaz("DMO-A240-0001", a100, klinik, new DateOnly(2025, 3, 10), null, HizmetSeviyesi.Plus),
            Cihaz("DMO-A230-0002", a100, klinik, new DateOnly(2023, 1, 15), new DateOnly(2025, 1, 15), HizmetSeviyesi.Standart),
            Cihaz("DMO-B250-0003", b200, spor, new DateOnly(2026, 10, 1), new DateOnly(2029, 10, 1), HizmetSeviyesi.Plus),
            Cihaz("DMO-C260-0004", c300, spor, new DateOnly(2026, 11, 2), null, HizmetSeviyesi.Standart));

        db.Kullanicilar.AddRange(
            Kullanici("musteri@ornek-klinik.test", "Ayşe Yılmaz", KullaniciRolu.Musteri, klinik),
            Kullanici("musteri@ornek-spor.test", "Mehmet Demir", KullaniciRolu.Musteri, spor),
            Kullanici("personel@servis.test", "Selin Kaya", KullaniciRolu.Personel, null),
            Kullanici("yonetici@servis.test", "Can Öztürk", KullaniciRolu.Yonetici, null));

        await db.SaveChangesAsync(ct);
    }

    private static Cihaz Cihaz(string seri, CihazModeli model, Kurum kurum, DateOnly kurulum, DateOnly? bitis, HizmetSeviyesi seviye) => new()
    {
        SeriNo = SeriNumarasi.Normalize(seri),
        Model = model,
        Kurum = kurum,
        KurulumTarihi = kurulum,
        GarantiBitis = bitis,
        HizmetSeviyesi = seviye,
    };

    private Kullanici Kullanici(string eposta, string ad, KullaniciRolu rol, Kurum? kurum)
    {
        var k = new Kullanici { Eposta = eposta, AdSoyad = ad, Rol = rol, Kurum = kurum };
        k.SifreOzeti = hasher.HashPassword(k, DemoSifre);
        return k;
    }
}
