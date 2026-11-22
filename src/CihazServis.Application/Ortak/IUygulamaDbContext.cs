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

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
