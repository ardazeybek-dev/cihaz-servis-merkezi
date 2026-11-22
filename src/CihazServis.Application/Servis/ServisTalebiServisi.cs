using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Servis;

/// <summary>Formda gösterilen ve onaylanan aydınlatma metninin sürümü. Metin değişince sürüm artırılır.</summary>
public static class AydinlatmaMetni
{
    public const string Surum = "1.0";
    public static readonly DateOnly Tarih = new(2026, 11, 20);
}

public sealed record TalepGirdisi(
    string? SeriNo,
    TalepKonusu Konu,
    string Aciklama,
    string AdSoyad,
    string? KurumAdi,
    string Telefon,
    string Eposta,
    string? AydinlatmaSurumu,
    bool AydinlatmaOnay,
    string? Tuzak,
    string? Ip);

/// <summary>Yüklenen dosya. <see cref="Ac"/> her çağrıda baştan okunan yeni bir akış döner.</summary>
public sealed record EkGirdisi(string Ad, long Boyut, Func<Stream> Ac);

/// <summary>Tuzak alan doluysa kayıt açılmaz, No boş döner ama yanıt başarılı görünür.</summary>
public sealed record TalepOlusturmaSonucu(string? No, DateOnly? HedefTarih);

public sealed record CizelgeAdimi(TalepDurumu Durum, string DurumAdi, DateTimeOffset Zaman, string? Not);

/// <summary>
/// Herkese açık durum yanıtı. Bilerek ad, telefon, e-posta, kurum, seri numarası ve iç not içermez.
/// </summary>
public sealed record TalepDurumYaniti(
    string No,
    TalepDurumu Durum,
    string DurumAdi,
    string Konu,
    string? ModelAdi,
    DateOnly AcilisTarihi,
    DateOnly? TahminiTarih,
    DateOnly? KapanisTarihi,
    string SiradakiAdim,
    IReadOnlyList<CizelgeAdimi> Cizelge);

public sealed record TalepSatiri(
    string No, DateTimeOffset OlusturmaZamani, TalepKonusu Konu, TalepDurumu Durum, string AdSoyad, string? KurumAdi,
    string SeriNo, bool Dogrulandi, string SorumluAd, DateOnly HedefTarih, bool Gecikti);

public sealed record TalepFiltresi(TalepDurumu? Durum = null, bool YalnizAcik = true, int? SorumluId = null, string? Arama = null);

public sealed record EkIcerigi(Stream Akis, string Ad, string IcerikTuru);

public sealed class ServisTalebiServisi(
    IUygulamaDbContext db,
    TimeProvider saat,
    ITalepNoUretici noUretici,
    IDosyaDeposu depo,
    IBildirimKuyrugu kuyruk,
    IIpOzetleyici ipOzetleyici)
{
    public const int AzamiEk = 3;
    public const long AzamiEkBoyutu = 10 * 1024 * 1024;
    public const long AzamiToplamEkBoyutu = 25 * 1024 * 1024;
    public const int AzamiHataliDeneme = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    private const string EslesmediMesaji = "Talep numarası ya da telefon numarasının son 4 hanesi eşleşmedi. Bilgileri kontrol edip tekrar deneyin.";

    public async Task<Sonuc<TalepOlusturmaSonucu>> OlusturAsync(TalepGirdisi g, IReadOnlyList<EkGirdisi> ekler, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(g.Tuzak))
        {
            // Bot olduğu belli: kayıt açılmaz, e-posta gitmez, yanıt yine başarılı görünür.
            return Sonuc<TalepOlusturmaSonucu>.Tamam(new TalepOlusturmaSonucu(null, null));
        }

        if (!g.AydinlatmaOnay)
        {
            return Sonuc<TalepOlusturmaSonucu>.Gecersiz("aydinlatma", "Devam etmek için aydınlatma metnini okuduğunuzu onaylayın.");
        }

        if (g.AydinlatmaSurumu != AydinlatmaMetni.Surum)
        {
            return Sonuc<TalepOlusturmaSonucu>.Gecersiz("aydinlatmaSurumu", "Aydınlatma metni güncellendi. Sayfayı yenileyip yeni metni onaylayın.");
        }

        var telefon = Telefon.Normalize(g.Telefon);
        if (telefon is null)
        {
            return Sonuc<TalepOlusturmaSonucu>.Gecersiz("telefon", "Telefon numarasını 0555 123 45 67 biçiminde yazın.");
        }

        var ekKontrol = EkleriDogrula(ekler);
        if (!ekKontrol.Basarili)
        {
            return Sonuc<TalepOlusturmaSonucu>.Gecersiz(ekKontrol.Kod!, ekKontrol.Mesaj!);
        }

        var seri = SeriNumarasi.Normalize(g.SeriNo);
        var cihaz = seri.Length == 0 ? null : await db.Cihazlar.Include(c => c.Model).FirstOrDefaultAsync(c => c.SeriNo == seri, ct);
        var sorumluId = await SorumluBulAsync(g.Konu, cihaz?.ModelId, ct);
        var simdi = saat.GetUtcNow();
        var hedef = IsGunuTakvimi.Ekle(TurkiyeSaati.Bugun(simdi), TalepKonusuBilgi.HedefIsGunu(g.Konu, cihaz?.HizmetSeviyesi));

        var kaydedilenler = new List<string>();
        try
        {
            var talep = new ServisTalebi
            {
                SeriNo = seri,
                Cihaz = cihaz,
                Dogrulandi = cihaz is not null,
                Konu = g.Konu,
                Aciklama = g.Aciklama.Trim(),
                AdSoyad = g.AdSoyad.Trim(),
                KurumAdi = string.IsNullOrWhiteSpace(g.KurumAdi) ? null : g.KurumAdi.Trim(),
                Telefon = telefon,
                Eposta = g.Eposta.Trim(),
                AydinlatmaSurumu = AydinlatmaMetni.Surum,
                AydinlatmaOnayZamani = simdi,
                Durum = TalepDurumu.Alindi,
                OlusturmaZamani = simdi,
                HedefTarih = hedef,
                SorumluId = sorumluId,
                IpOzeti = ipOzetleyici.Ozetle(g.Ip),
            };

            foreach (var (ek, tur) in ekKontrol.Deger!)
            {
                await using var akis = ek.Ac();
                var depoAdi = await depo.KaydetAsync(akis, ct);
                kaydedilenler.Add(depoAdi);
                talep.Ekler.Add(new TalepEki { OrijinalAd = GuvenliAd(ek.Ad), DepoAdi = depoAdi, IcerikTuru = tur.IcerikTuru, Boyut = ek.Boyut });
            }

            talep.Hareketler.Add(new TalepHareketi { EskiDurum = null, YeniDurum = TalepDurumu.Alindi, Zaman = simdi });
            talep.No = await noUretici.SonrakiAsync(TurkiyeSaati.Yerel(simdi).Year, ct);
            db.ServisTalepleri.Add(talep);
            db.Denetle(simdi, null, "TalepOlusturuldu", "ServisTalebi", talep.No,
                $"konu={g.Konu}; dogrulandi={talep.Dogrulandi}; ek={talep.Ekler.Count}; aydinlatma={AydinlatmaMetni.Surum}");
            await db.SaveChangesAsync(ct);

            kuyruk.TalepAlindi(talep.Id);
            return Sonuc<TalepOlusturmaSonucu>.Tamam(new TalepOlusturmaSonucu(talep.No, hedef));
        }
        catch
        {
            foreach (var d in kaydedilenler)
            {
                depo.Sil(d);
            }

            throw;
        }
    }

    private static Sonuc<List<(EkGirdisi, DosyaTuru)>> EkleriDogrula(IReadOnlyList<EkGirdisi> ekler)
    {
        if (ekler.Count > AzamiEk)
        {
            return Sonuc<List<(EkGirdisi, DosyaTuru)>>.Gecersiz("ekSayisi", $"En fazla {AzamiEk} dosya ekleyebilirsiniz.");
        }

        if (ekler.Sum(e => e.Boyut) > AzamiToplamEkBoyutu)
        {
            return Sonuc<List<(EkGirdisi, DosyaTuru)>>.Gecersiz("ekBoyutu", $"Eklerin toplamı {AzamiToplamEkBoyutu / 1024 / 1024} MB'ı geçemez.");
        }

        var liste = new List<(EkGirdisi, DosyaTuru)>();
        Span<byte> bas = stackalloc byte[DosyaImzasi.OkunacakBayt];
        foreach (var ek in ekler)
        {
            var ad = GuvenliAd(ek.Ad);
            if (ek.Boyut <= 0 || ek.Boyut > AzamiEkBoyutu)
            {
                return Sonuc<List<(EkGirdisi, DosyaTuru)>>.Gecersiz("ekBoyutu", $"{ad} dosyası boş ya da {AzamiEkBoyutu / 1024 / 1024} MB'tan büyük.");
            }

            int okunan;
            using (var akis = ek.Ac())
            {
                okunan = akis.ReadAtLeast(bas, bas.Length, throwOnEndOfStream: false);
            }

            var tur = DosyaImzasi.Dogrula(ad, bas[..okunan]);
            if (tur is null)
            {
                return Sonuc<List<(EkGirdisi, DosyaTuru)>>.Gecersiz("ekTuru",
                    $"{ad} dosyasının türü tanınmadı ya da uzantısı içeriğiyle uyuşmuyor. JPG, PNG, PDF ya da MP4 ekleyebilirsiniz.");
            }

            liste.Add((ek, tur));
        }

        return Sonuc<List<(EkGirdisi, DosyaTuru)>>.Tamam(liste);
    }

    /// <summary>Yoldan arındırılmış, kısaltılmış dosya adı. Depoda bu ad kullanılmaz.</summary>
    public static string GuvenliAd(string ad)
    {
        var yalin = Path.GetFileName(ad.Replace('\\', '/'));
        var temiz = string.Concat(yalin.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or ':' or '*' or '?')));
        if (temiz.Length > 120)
        {
            var uzanti = Path.GetExtension(temiz);
            temiz = temiz[..(120 - uzanti.Length)] + uzanti;
        }

        return temiz.Length == 0 ? "dosya" : temiz;
    }

    /// <summary>Kurallar öncelik sırasıyla denenir; hiçbiri uymazsa ilk yönetici. Sahipsiz talep oluşmaz.</summary>
    public async Task<int> SorumluBulAsync(TalepKonusu konu, int? modelId, CancellationToken ct = default)
    {
        var kural = await db.AtamaKurallari.AsNoTracking()
            .Where(k => k.Etkin && (k.Konu == null || k.Konu == konu) && (k.ModelId == null || k.ModelId == modelId))
            .OrderBy(k => k.Oncelik).ThenBy(k => k.Id)
            .Select(k => (int?)k.SorumluId)
            .FirstOrDefaultAsync(ct);
        if (kural is { } id)
        {
            return id;
        }

        return await db.Kullanicilar.Where(k => k.Rol == KullaniciRolu.Yonetici).OrderBy(k => k.Id).Select(k => (int?)k.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Talepleri üstlenecek bir yönetici hesabı tanımlı değil.");
    }

    /// <summary>
    /// Talep numarası + telefonun son 4 hanesiyle durum sorgusu. Kayıt yoksa da hane yanlışsa da aynı
    /// yanıt döner ve aynı numara için hatalı deneme sayılır; 5 hatada numara 15 dakika kilitlenir.
    /// </summary>
    public async Task<Sonuc<TalepDurumYaniti>> DurumSorgulaAsync(string? talepNo, string? telefonSon4, CancellationToken ct = default)
    {
        var no = TalepNumarasi.Normalize(talepNo);
        var son4 = Telefon.Son4Normalize(telefonSon4);
        if (no is null || son4 is null)
        {
            return Sonuc<TalepDurumYaniti>.Gecersiz("bicim", "Talep numarasını SR-2026-000001 biçiminde, telefonun son 4 hanesini rakamla yazın.");
        }

        var simdi = saat.GetUtcNow();
        var kilit = await db.DurumSorguKilitleri.AsNoTracking().FirstOrDefaultAsync(k => k.TalepNo == no, ct);
        if (kilit?.KilitBitis is { } bitis)
        {
            if (bitis > simdi)
            {
                var dakika = (int)Math.Ceiling((bitis - simdi).TotalMinutes);
                return Sonuc<TalepDurumYaniti>.Yasak("kilitli",
                    $"Bu talep numarası için çok fazla hatalı deneme yapıldı. {dakika} dakika sonra yeniden deneyebilirsiniz.");
            }

            // Kilit süresi doldu: sayaç sıfırdan başlar. Temizleme işinin çalışmış olması gerekmez.
            await db.DurumSorguKilitleri.Where(k => k.TalepNo == no && k.KilitBitis != null && k.KilitBitis <= simdi)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.HataliDeneme, 0).SetProperty(k => k.KilitBitis, (DateTimeOffset?)null), ct);
        }

        var talep = await db.ServisTalepleri.AsNoTracking()
            .Where(t => t.No == no)
            .Select(t => new { t.Id, t.Telefon, t.Durum, t.Konu, t.OlusturmaZamani, t.HedefTarih, ModelAdi = t.Cihaz != null ? t.Cihaz.Model.Ad : null })
            .FirstOrDefaultAsync(ct);
        if (talep is null || !talep.Telefon.EndsWith(son4, StringComparison.Ordinal))
        {
            await HataliDenemeSayAsync(no, simdi, ct);
            return Sonuc<TalepDurumYaniti>.Bulunamadi(EslesmediMesaji);
        }

        if (kilit is not null)
        {
            await db.DurumSorguKilitleri.Where(k => k.TalepNo == no).ExecuteDeleteAsync(ct);
        }

        var hareketler = await db.TalepHareketleri.AsNoTracking()
            .Where(h => h.TalepId == talep.Id)
            .OrderBy(h => h.Zaman).ThenBy(h => h.Id)
            .Select(h => new { h.EskiDurum, h.YeniDurum, h.Zaman, h.MusteriNotu })
            .ToListAsync(ct);
        var cizelge = hareketler
            .Where(h => h.EskiDurum != h.YeniDurum || h.MusteriNotu != null)
            .Select(h => new CizelgeAdimi(h.YeniDurum, TalepDurumAkisi.Ad(h.YeniDurum), h.Zaman, h.MusteriNotu))
            .ToList();
        var kapali = TalepDurumAkisi.KapaliMi(talep.Durum);
        DateOnly? kapanis = kapali ? TurkiyeSaati.Bugun(hareketler.Last(h => h.YeniDurum == talep.Durum).Zaman) : null;

        return Sonuc<TalepDurumYaniti>.Tamam(new TalepDurumYaniti(
            no, talep.Durum, TalepDurumAkisi.Ad(talep.Durum), TalepKonusuBilgi.Ad(talep.Konu), talep.ModelAdi,
            TurkiyeSaati.Bugun(talep.OlusturmaZamani), kapali ? null : talep.HedefTarih, kapanis,
            TalepDurumAkisi.SiradakiAdim(talep.Durum), cizelge));
    }

    /// <summary>Sayaç tek SQL cümlesiyle artırılır, eşzamanlı denemeler birbirinin artışını ezmez.</summary>
    private async Task HataliDenemeSayAsync(string no, DateTimeOffset simdi, CancellationToken ct)
    {
        var bitis = simdi.Add(KilitSuresi);
        for (var deneme = 0; deneme < 3; deneme++)
        {
            var etkilenen = await db.DurumSorguKilitleri.Where(k => k.TalepNo == no)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(k => k.HataliDeneme, k => k.HataliDeneme + 1)
                    .SetProperty(k => k.SonHata, simdi)
                    .SetProperty(k => k.KilitBitis, k => k.HataliDeneme + 1 >= AzamiHataliDeneme ? bitis : k.KilitBitis), ct);
            if (etkilenen > 0)
            {
                return;
            }

            var yeni = new DurumSorguKilidi { TalepNo = no, HataliDeneme = 1, SonHata = simdi };
            db.DurumSorguKilitleri.Add(yeni);
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException)
            {
                // Aynı anda başka bir istek satırı ekledi: bu satırı bırakıp güncellemeyi yeniden dene.
                db.DurumSorguKilitleri.Remove(yeni);
            }
        }
    }

    public async Task<Sonuc<long>> DurumDegistirAsync(
        string talepNo, TalepDurumu yeniDurum, TalepDurumu beklenenDurum, string? musteriNotu, string? icNot, int yapanKullaniciId,
        CancellationToken ct = default)
    {
        var no = TalepNumarasi.Normalize(talepNo);
        var talep = no is null ? null : await db.ServisTalepleri.FirstOrDefaultAsync(t => t.No == no, ct);
        if (talep is null)
        {
            return Sonuc<long>.Bulunamadi("Talep bulunamadı.");
        }

        if (talep.Durum != beklenenDurum)
        {
            return Sonuc<long>.Cakisma("guncelDegil", "Talep siz bakarken başka biri tarafından güncellendi. Sayfayı yenileyip tekrar deneyin.");
        }

        musteriNotu = string.IsNullOrWhiteSpace(musteriNotu) ? null : musteriNotu.Trim();
        icNot = string.IsNullOrWhiteSpace(icNot) ? null : icNot.Trim();
        var durumDegisti = yeniDurum != talep.Durum;
        if (durumDegisti && !TalepDurumAkisi.GecebilirMi(talep.Durum, yeniDurum))
        {
            return Sonuc<long>.Gecersiz("gecersizGecis",
                $"{TalepDurumAkisi.Ad(talep.Durum)} durumundaki talep {TalepDurumAkisi.Ad(yeniDurum)} durumuna geçirilemez.");
        }

        if (!durumDegisti && musteriNotu is null && icNot is null)
        {
            return Sonuc<long>.Gecersiz("bosIslem", "Durum değişmedi ve not yazılmadı.");
        }

        var simdi = saat.GetUtcNow();
        var hareket = new TalepHareketi
        {
            TalepId = talep.Id,
            EskiDurum = talep.Durum,
            YeniDurum = yeniDurum,
            Zaman = simdi,
            MusteriNotu = musteriNotu,
            IcNot = icNot,
            YapanKullaniciId = yapanKullaniciId,
        };
        db.TalepHareketleri.Add(hareket);
        db.Denetle(simdi, yapanKullaniciId, durumDegisti ? "DurumDegisti" : "NotEklendi", "ServisTalebi", talep.No,
            $"{talep.Durum} -> {yeniDurum}; musteriNotu={(musteriNotu is null ? "yok" : "var")}; icNot={(icNot is null ? "yok" : "var")}");
        talep.Durum = yeniDurum;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Sonuc<long>.Cakisma("guncelDegil", "Talep siz bakarken başka biri tarafından güncellendi. Sayfayı yenileyip tekrar deneyin.");
        }

        if (durumDegisti || musteriNotu is not null)
        {
            kuyruk.DurumDegisti(hareket.Id);
        }

        return Sonuc<long>.Tamam(hareket.Id);
    }

    public async Task<List<TalepSatiri>> ListeleAsync(TalepFiltresi f, CancellationToken ct = default)
    {
        var bugun = TurkiyeSaati.Bugun(saat.GetUtcNow());
        var q = db.ServisTalepleri.AsNoTracking().AsQueryable();
        if (f.Durum is { } d)
        {
            q = q.Where(t => t.Durum == d);
        }
        else if (f.YalnizAcik)
        {
            q = q.Where(t => t.Durum != TalepDurumu.Tamamlandi && t.Durum != TalepDurumu.Iptal);
        }

        if (f.SorumluId is { } s)
        {
            q = q.Where(t => t.SorumluId == s);
        }

        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var no = TalepNumarasi.Normalize(f.Arama);
            var seri = SeriNumarasi.Normalize(f.Arama);
            var metin = f.Arama.Trim();
            q = q.Where(t => t.No == no || t.SeriNo == seri || t.AdSoyad.Contains(metin) || (t.KurumAdi != null && t.KurumAdi.Contains(metin)));
        }

        return await q.OrderBy(t => t.HedefTarih).ThenBy(t => t.Id).Take(200)
            .Select(t => new TalepSatiri(t.No, t.OlusturmaZamani, t.Konu, t.Durum, t.AdSoyad, t.KurumAdi, t.SeriNo, t.Dogrulandi,
                t.Sorumlu.AdSoyad, t.HedefTarih,
                t.HedefTarih < bugun && t.Durum != TalepDurumu.Tamamlandi && t.Durum != TalepDurumu.Iptal))
            .ToListAsync(ct);
    }

    public Task<ServisTalebi?> DetayAsync(string talepNo, CancellationToken ct = default)
    {
        var no = TalepNumarasi.Normalize(talepNo);
        return db.ServisTalepleri.AsNoTracking()
            .Include(t => t.Cihaz).ThenInclude(c => c!.Model)
            .Include(t => t.Cihaz).ThenInclude(c => c!.Kurum)
            .Include(t => t.Sorumlu)
            .Include(t => t.Ekler)
            .Include(t => t.Hareketler).ThenInclude(h => h.YapanKullanici)
            .FirstOrDefaultAsync(t => t.No == no, ct);
    }

    /// <summary>Ek yalnız kendi talebinin numarasıyla açılır; her indirme denetim günlüğüne yazılır.</summary>
    public async Task<EkIcerigi?> EkAcAsync(string talepNo, int ekId, int yapanKullaniciId, CancellationToken ct = default)
    {
        var no = TalepNumarasi.Normalize(talepNo);
        var ek = await db.TalepEkleri.AsNoTracking()
            .Where(e => e.Id == ekId && db.ServisTalepleri.Any(t => t.Id == e.TalepId && t.No == no))
            .FirstOrDefaultAsync(ct);
        var akis = ek is null ? null : depo.Ac(ek.DepoAdi);
        if (ek is null || akis is null)
        {
            return null;
        }

        db.Denetle(saat.GetUtcNow(), yapanKullaniciId, "EkIndirildi", "TalepEki", no, $"ek={ek.Id}");
        await db.SaveChangesAsync(ct);
        return new EkIcerigi(akis, ek.OrijinalAd, ek.IcerikTuru);
    }
}

/// <summary>Süresi dolan kilitleri ve bir günden eski, kilide dönüşmemiş hatalı deneme kayıtlarını siler.</summary>
public sealed class DurumKilidiTemizleyici(IUygulamaDbContext db, TimeProvider saat)
{
    public Task<int> TemizleAsync(CancellationToken ct = default)
    {
        var simdi = saat.GetUtcNow();
        var gunOnce = simdi.AddDays(-1);
        return db.DurumSorguKilitleri
            .Where(k => (k.KilitBitis != null && k.KilitBitis <= simdi) || (k.KilitBitis == null && k.SonHata < gunOnce))
            .ExecuteDeleteAsync(ct);
    }
}

public static class DenetimUzantisi
{
    /// <summary>Kaydı ekler, kaydetmez: işlemle aynı SaveChanges içinde yazılır.</summary>
    public static void Denetle(this IUygulamaDbContext db, DateTimeOffset zaman, int? kullaniciId, string islem, string varlik, string? no, string? ayrinti)
        => db.DenetimKayitlari.Add(new DenetimKaydi
        {
            Zaman = zaman,
            KullaniciId = kullaniciId,
            Islem = islem,
            Varlik = varlik,
            VarlikNo = no,
            Ayrinti = ayrinti,
        });
}
