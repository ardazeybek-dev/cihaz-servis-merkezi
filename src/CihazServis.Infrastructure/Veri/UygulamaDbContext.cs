using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Infrastructure.Veri;

public class UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : DbContext(options), IUygulamaDbContext
{
    /// <summary>Tüm tablolar bu şemada durur, ana platform veritabanıyla karışmaz.</summary>
    public const string Sema = "servis";

    public DbSet<Kurum> Kurumlar => Set<Kurum>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<CihazModeli> CihazModelleri => Set<CihazModeli>();
    public DbSet<Cihaz> Cihazlar => Set<Cihaz>();
    public DbSet<DogrulamaSorgusu> DogrulamaSorgulari => Set<DogrulamaSorgusu>();
    public DbSet<Kurs> Kurslar => Set<Kurs>();
    public DbSet<Ders> Dersler => Set<Ders>();
    public DbSet<QuizSorusu> QuizSorulari => Set<QuizSorusu>();
    public DbSet<QuizSecenegi> QuizSecenekleri => Set<QuizSecenegi>();
    public DbSet<DersIlerlemesi> DersIlerlemeleri => Set<DersIlerlemesi>();
    public DbSet<QuizDenemesi> QuizDenemeleri => Set<QuizDenemesi>();
    public DbSet<Sertifika> Sertifikalar => Set<Sertifika>();
    public DbSet<SertifikaHareketi> SertifikaHareketleri => Set<SertifikaHareketi>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Sema);
        b.ApplyConfigurationsFromAssembly(typeof(UygulamaDbContext).Assembly);
    }
}
