using CihazServis.Application.Egitim;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CihazServis.Infrastructure.Veri.Yapilandirma;

internal sealed class KursYapilandirma : IEntityTypeConfiguration<Kurs>
{
    public void Configure(EntityTypeBuilder<Kurs> e)
    {
        e.ToTable("Kurslar");
        e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
        e.Property(x => x.Aciklama).HasMaxLength(1000);
        e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Dersler).WithOne(x => x.Kurs).HasForeignKey(x => x.KursId);
    }
}

internal sealed class DersYapilandirma : IEntityTypeConfiguration<Ders>
{
    public void Configure(EntityTypeBuilder<Ders> e)
    {
        e.ToTable("Dersler");
        e.Property(x => x.Baslik).HasMaxLength(200).IsRequired();
        e.Property(x => x.Aciklama).HasMaxLength(1000);
        e.Property(x => x.MedyaAnahtari).HasMaxLength(64).IsRequired();
        e.HasIndex(x => new { x.KursId, x.Sira }).IsUnique();
        e.HasMany(x => x.Sorular).WithOne().HasForeignKey(x => x.DersId);
    }
}

internal sealed class QuizSorusuYapilandirma : IEntityTypeConfiguration<QuizSorusu>
{
    public void Configure(EntityTypeBuilder<QuizSorusu> e)
    {
        e.ToTable("QuizSorulari");
        e.Property(x => x.Metin).HasMaxLength(500).IsRequired();
        e.HasMany(x => x.Secenekler).WithOne().HasForeignKey(x => x.SoruId);
    }
}

internal sealed class QuizSecenegiYapilandirma : IEntityTypeConfiguration<QuizSecenegi>
{
    public void Configure(EntityTypeBuilder<QuizSecenegi> e)
    {
        e.ToTable("QuizSecenekleri");
        e.Property(x => x.Metin).HasMaxLength(300).IsRequired();
    }
}

internal sealed class DersIlerlemesiYapilandirma : IEntityTypeConfiguration<DersIlerlemesi>
{
    public void Configure(EntityTypeBuilder<DersIlerlemesi> e)
    {
        e.ToTable("DersIlerlemeleri");
        e.HasIndex(x => new { x.KullaniciId, x.DersId }).IsUnique();
        e.Property(x => x.IzlenenAraliklar).HasMaxLength(4000).IsRequired();
        e.Property(x => x.SurumDamgasi).IsRowVersion();
        e.HasOne(x => x.Ders).WithMany().HasForeignKey(x => x.DersId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Kullanici>().WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizDenemesiYapilandirma : IEntityTypeConfiguration<QuizDenemesi>
{
    public void Configure(EntityTypeBuilder<QuizDenemesi> e)
    {
        e.ToTable("QuizDenemeleri");
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.SoruDizilimi).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => new { x.KullaniciId, x.DersId, x.Dongu });
        e.HasOne<Ders>().WithMany().HasForeignKey(x => x.DersId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Kullanici>().WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SertifikaYapilandirma : IEntityTypeConfiguration<Sertifika>
{
    public void Configure(EntityTypeBuilder<Sertifika> e)
    {
        e.ToTable("Sertifikalar");
        e.Property(x => x.No).HasMaxLength(SertifikaServisi.NoUzunlugu).IsFixedLength().IsUnicode(false).IsRequired();
        e.HasIndex(x => x.No).IsUnique();
        e.HasIndex(x => new { x.KullaniciId, x.KursId }).IsUnique();
        e.Property(x => x.AdSoyad).HasMaxLength(150).IsRequired();
        e.HasOne(x => x.Kurs).WithMany().HasForeignKey(x => x.KursId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Kullanici>().WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Hareketler).WithOne().HasForeignKey(x => x.SertifikaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SertifikaHareketiYapilandirma : IEntityTypeConfiguration<SertifikaHareketi>
{
    public void Configure(EntityTypeBuilder<SertifikaHareketi> e)
    {
        // Tetikleyici olduğunu EF'e bildirmek gerekir, yoksa ekleme sırasında OUTPUT kullanır ve hata alır.
        e.ToTable("SertifikaHareketleri", t => t.HasTrigger("TR_SertifikaHareketleri_YalnizEkleme"));
        e.Property(x => x.Tur).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Aciklama).HasMaxLength(500);
    }
}
