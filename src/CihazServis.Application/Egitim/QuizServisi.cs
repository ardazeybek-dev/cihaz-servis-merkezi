using System.Security.Cryptography;
using System.Text.Json;
using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Egitim;

/// <summary>İstemciye giden soru: doğru cevap bilgisi bilerek yok.</summary>
public sealed record QuizSorusuDto(int Id, string Metin, IReadOnlyList<SecenekDto> Secenekler);

public sealed record SecenekDto(int Id, string Metin);

public sealed record QuizDto(Guid DenemeId, int DersId, string DersBasligi, IReadOnlyList<QuizSorusuDto> Sorular, int KalanHak, int GecmeNotu);

public sealed record QuizSonucu(int Puan, bool Gecti, int DogruSayisi, int SoruSayisi, int KalanHak, bool VideoSifirlandi, string? SertifikaNo);

public sealed class QuizServisi(IUygulamaDbContext db, TimeProvider saat, EgitimServisi egitim, SertifikaServisi sertifika)
{
    public const int SoruSayisi = 5;
    public const int GecmeNotu = 80;
    public const int HakSayisi = 3;

    private sealed record Dizilim(int S, int[] O);

    public async Task<Sonuc<QuizDto>> BaslatAsync(int kullaniciId, int dersId, CancellationToken ct = default)
    {
        var durum = await egitim.DersDurumuAsync(kullaniciId, dersId, ct);
        if (durum is null)
        {
            return Sonuc<QuizDto>.Bulunamadi("Ders bulunamadı.");
        }

        switch (durum.Value.Durum.Asama)
        {
            case DersAsamasi.Kilitli:
            case DersAsamasi.VideoAcik:
                return Sonuc<QuizDto>.Yasak("videoTamamlanmadi", "Soruları görmek için önce dersin videosunu tamamlayın.");
            case DersAsamasi.Tamamlandi:
                return Sonuc<QuizDto>.Cakisma("quizGecildi", "Bu dersin sorularını zaten geçtiniz.");
        }

        var ders = durum.Value.Ders;
        var i = await egitim.IlerlemeAsync(kullaniciId, dersId, ct);
        var denemeler = await db.QuizDenemeleri
            .Where(d => d.KullaniciId == kullaniciId && d.DersId == dersId && d.Dongu == i.Dongu).ToListAsync(ct);
        var kalanHak = HakSayisi - denemeler.Count(d => d.Bitis is not null);

        // Yarıda kalan deneme varsa aynı sorular döner: sayfayı yenileyerek soru değiştirilemez.
        var deneme = denemeler.FirstOrDefault(d => d.Bitis is null);
        if (deneme is null)
        {
            var havuz = await db.QuizSorulari.AsNoTracking().Include(s => s.Secenekler)
                .Where(s => s.DersId == dersId).ToArrayAsync(ct);
            RandomNumberGenerator.Shuffle(havuz.AsSpan());
            var secilen = havuz.Take(SoruSayisi).Select(s =>
            {
                var secenekler = s.Secenekler.Select(x => x.Id).ToArray();
                RandomNumberGenerator.Shuffle(secenekler.AsSpan());
                return new Dizilim(s.Id, secenekler);
            }).ToList();

            deneme = new QuizDenemesi
            {
                Id = Guid.NewGuid(),
                KullaniciId = kullaniciId,
                DersId = dersId,
                Dongu = i.Dongu,
                Baslangic = saat.GetUtcNow(),
                SoruDizilimi = JsonSerializer.Serialize(secilen),
                SoruSayisi = secilen.Count,
            };
            db.QuizDenemeleri.Add(deneme);
            await db.SaveChangesAsync(ct);
        }

        return Sonuc<QuizDto>.Tamam(await DtoAsync(deneme, ders, kalanHak, ct));
    }

    public async Task<Sonuc<QuizSonucu>> CevaplaAsync(int kullaniciId, Guid denemeId, IReadOnlyDictionary<int, int> cevaplar, CancellationToken ct = default)
    {
        // Kullanıcı süzgeci sorgunun içinde: başkasının denemesi "yok" görünür.
        var deneme = await db.QuizDenemeleri.FirstOrDefaultAsync(d => d.Id == denemeId && d.KullaniciId == kullaniciId, ct);
        if (deneme is null)
        {
            return Sonuc<QuizSonucu>.Bulunamadi("Deneme bulunamadı.");
        }

        if (deneme.Bitis is not null)
        {
            return Sonuc<QuizSonucu>.Cakisma("denemeBitti", "Bu deneme daha önce gönderildi.");
        }

        var i = await egitim.IlerlemeAsync(kullaniciId, deneme.DersId, ct);
        if (i.Dongu != deneme.Dongu || i.VideoTamamlandi is null || i.QuizGecildi is not null)
        {
            return Sonuc<QuizSonucu>.Cakisma("denemeGecersiz", "Bu deneme artık geçerli değil, soruları yeniden açın.");
        }

        var dizilim = JsonSerializer.Deserialize<List<Dizilim>>(deneme.SoruDizilimi) ?? [];
        var soruIdleri = dizilim.Select(d => d.S).ToList();
        var dogrular = await db.QuizSecenekleri.AsNoTracking()
            .Where(s => soruIdleri.Contains(s.SoruId) && s.Dogru)
            .Select(s => new { s.SoruId, s.Id }).ToListAsync(ct);

        var dogruSayisi = dizilim.Count(d =>
            cevaplar.TryGetValue(d.S, out var secilen) && d.O.Contains(secilen) && dogrular.Any(x => x.SoruId == d.S && x.Id == secilen));
        var puan = deneme.SoruSayisi == 0 ? 0 : dogruSayisi * 100 / deneme.SoruSayisi;
        var gecti = puan >= GecmeNotu;

        deneme.Bitis = saat.GetUtcNow();
        deneme.DogruSayisi = dogruSayisi;
        deneme.Gecti = gecti;

        var bitenDeneme = await db.QuizDenemeleri.CountAsync(d =>
            d.KullaniciId == kullaniciId && d.DersId == deneme.DersId && d.Dongu == deneme.Dongu && d.Bitis != null, ct) + 1;
        var kalanHak = Math.Max(0, HakSayisi - bitenDeneme);
        var sifirlandi = false;
        string? sertifikaNo = null;

        if (gecti)
        {
            i.QuizGecildi = saat.GetUtcNow();
            sertifikaNo = await sertifika.KursBittiyseVerAsync(kullaniciId, deneme.DersId, ct);
        }
        else if (kalanHak == 0)
        {
            // Haklar bitti: video baştan izlenmeli.
            i.VideoTamamlandi = null;
            i.IzlenenAraliklar = "[]";
            i.IzlenenSaniye = 0;
            i.EnIleriSaniye = 0;
            i.AktifOturum = null;
            i.Dongu++;
            sifirlandi = true;
        }

        await db.SaveChangesAsync(ct);
        return Sonuc<QuizSonucu>.Tamam(new QuizSonucu(puan, gecti, dogruSayisi, deneme.SoruSayisi, kalanHak, sifirlandi, sertifikaNo));
    }

    private async Task<QuizDto> DtoAsync(QuizDenemesi deneme, Ders ders, int kalanHak, CancellationToken ct)
    {
        var dizilim = JsonSerializer.Deserialize<List<Dizilim>>(deneme.SoruDizilimi) ?? [];
        var idler = dizilim.Select(d => d.S).ToList();
        var sorular = await db.QuizSorulari.AsNoTracking().Include(s => s.Secenekler)
            .Where(s => idler.Contains(s.Id)).ToListAsync(ct);
        var dto = dizilim.Select(d =>
        {
            var s = sorular.First(x => x.Id == d.S);
            return new QuizSorusuDto(s.Id, s.Metin,
                d.O.Select(o => s.Secenekler.First(x => x.Id == o)).Select(x => new SecenekDto(x.Id, x.Metin)).ToList());
        }).ToList();
        return new QuizDto(deneme.Id, ders.Id, ders.Baslik, dto, kalanHak, GecmeNotu);
    }
}
