using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Application.Ortak;

/// <summary>
/// Uygulama servislerinin veriye eriştiği tek kapı. Servisler somut DbContext'i
/// bilmez, bu sayede veri katmanı ileride başka bir veritabanına taşınabilir.
/// </summary>
public interface IUygulamaDbContext
{
    DbSet<Kurum> Kurumlar { get; }
    DbSet<Kullanici> Kullanicilar { get; }
    DbSet<CihazModeli> CihazModelleri { get; }
    DbSet<Cihaz> Cihazlar { get; }
    DbSet<DogrulamaSorgusu> DogrulamaSorgulari { get; }
    DbSet<Kurs> Kurslar { get; }
    DbSet<Ders> Dersler { get; }
    DbSet<QuizSorusu> QuizSorulari { get; }
    DbSet<QuizSecenegi> QuizSecenekleri { get; }
    DbSet<DersIlerlemesi> DersIlerlemeleri { get; }
    DbSet<QuizDenemesi> QuizDenemeleri { get; }
    DbSet<Sertifika> Sertifikalar { get; }
    DbSet<SertifikaHareketi> SertifikaHareketleri { get; }
    DbSet<ServisTalebi> ServisTalepleri { get; }
    DbSet<TalepEki> TalepEkleri { get; }
    DbSet<TalepHareketi> TalepHareketleri { get; }
    DbSet<AtamaKurali> AtamaKurallari { get; }
    DbSet<TalepSayaci> TalepSayaclari { get; }
    DbSet<DurumSorguKilidi> DurumSorguKilitleri { get; }
    DbSet<DenetimKaydi> DenetimKayitlari { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
