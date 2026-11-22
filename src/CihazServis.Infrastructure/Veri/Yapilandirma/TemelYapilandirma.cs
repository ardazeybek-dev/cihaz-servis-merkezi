using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CihazServis.Infrastructure.Veri.Yapilandirma;

internal sealed class KurumYapilandirma : IEntityTypeConfiguration<Kurum>
{
    public void Configure(EntityTypeBuilder<Kurum> e)
    {
        e.ToTable("Kurumlar");
        e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
    }
}

internal sealed class KullaniciYapilandirma : IEntityTypeConfiguration<Kullanici>
{
    public void Configure(EntityTypeBuilder<Kullanici> e)
    {
        e.ToTable("Kullanicilar");
        e.Property(x => x.Eposta).HasMaxLength(254).IsRequired();
        e.HasIndex(x => x.Eposta).IsUnique();
        e.Property(x => x.AdSoyad).HasMaxLength(150).IsRequired();
        e.Property(x => x.SifreOzeti).HasMaxLength(500).IsRequired();
        e.Property(x => x.Rol).HasConversion<string>().HasMaxLength(20);
        e.HasOne(x => x.Kurum).WithMany().HasForeignKey(x => x.KurumId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CihazModeliYapilandirma : IEntityTypeConfiguration<CihazModeli>
{
    public void Configure(EntityTypeBuilder<CihazModeli> e)
    {
        e.ToTable("CihazModelleri");
        e.Property(x => x.Kod).HasMaxLength(20).IsRequired();
        e.HasIndex(x => x.Kod).IsUnique();
        e.Property(x => x.Ad).HasMaxLength(150).IsRequired();
        e.Property(x => x.SeriNoDeseni).HasMaxLength(200).IsRequired();
    }
}

internal sealed class DogrulamaSorgusuYapilandirma : IEntityTypeConfiguration<DogrulamaSorgusu>
{
    public void Configure(EntityTypeBuilder<DogrulamaSorgusu> e)
    {
        e.ToTable("DogrulamaSorgulari");
        e.Property(x => x.IpOzeti).HasMaxLength(32).IsRequired();
        e.Property(x => x.SeriNo).HasMaxLength(SeriNumarasi.AzamiUzunluk);
        e.Property(x => x.Sonuc).HasMaxLength(20).IsRequired();
        e.HasIndex(x => x.Zaman);
    }
}

internal sealed class CihazYapilandirma : IEntityTypeConfiguration<Cihaz>
{
    public void Configure(EntityTypeBuilder<Cihaz> e)
    {
        e.ToTable("Cihazlar", t => t.HasCheckConstraint("CK_Cihazlar_SeriNo_Normalize",
            "[SeriNo] = UPPER([SeriNo]) COLLATE Latin1_General_BIN2 AND [SeriNo] NOT LIKE '%[- ]%'"));
        e.Property(x => x.SeriNo).HasMaxLength(SeriNumarasi.AzamiUzunluk).IsRequired();
        e.HasIndex(x => x.SeriNo).IsUnique();
        e.Property(x => x.HizmetSeviyesi).HasConversion<string>().HasMaxLength(20);
        e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Kurum).WithMany().HasForeignKey(x => x.KurumId).OnDelete(DeleteBehavior.Restrict);
    }
}
