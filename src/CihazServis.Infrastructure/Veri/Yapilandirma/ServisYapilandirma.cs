using CihazServis.Application.Ortak;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CihazServis.Infrastructure.Veri.Yapilandirma;

internal sealed class ServisTalebiYapilandirma : IEntityTypeConfiguration<ServisTalebi>
{
    public void Configure(EntityTypeBuilder<ServisTalebi> e)
    {
        e.ToTable("ServisTalepleri", t =>
        {
            t.HasCheckConstraint("CK_ServisTalepleri_No", "[No] LIKE 'SR-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]'");
            t.HasCheckConstraint("CK_ServisTalepleri_Telefon", "LEN([Telefon]) = 10 AND [Telefon] NOT LIKE '%[^0-9]%'");
        });
        e.Property(x => x.No).HasMaxLength(TalepNumarasi.Uzunluk).IsFixedLength().IsUnicode(false).IsRequired();
        e.HasIndex(x => x.No).IsUnique();
        e.Property(x => x.SeriNo).HasMaxLength(SeriNumarasi.AzamiUzunluk).IsRequired();
        e.HasIndex(x => x.SeriNo);
        e.Property(x => x.Konu).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Durum).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.Durum, x.HedefTarih });
        e.Property(x => x.Aciklama).HasMaxLength(2000).IsRequired();
        e.Property(x => x.AdSoyad).HasMaxLength(150).IsRequired();
        e.Property(x => x.KurumAdi).HasMaxLength(200);
        e.Property(x => x.Telefon).HasMaxLength(10).IsFixedLength().IsUnicode(false).IsRequired();
        e.Property(x => x.Eposta).HasMaxLength(254).IsRequired();
        e.Property(x => x.AydinlatmaSurumu).HasMaxLength(10).IsRequired();
        e.Property(x => x.IpOzeti).HasMaxLength(32).IsRequired();
        e.Property(x => x.SurumDamgasi).IsRowVersion();
        e.HasOne(x => x.Cihaz).WithMany().HasForeignKey(x => x.CihazId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Sorumlu).WithMany().HasForeignKey(x => x.SorumluId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Ekler).WithOne().HasForeignKey(x => x.TalepId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Hareketler).WithOne().HasForeignKey(x => x.TalepId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TalepEkiYapilandirma : IEntityTypeConfiguration<TalepEki>
{
    public void Configure(EntityTypeBuilder<TalepEki> e)
    {
        e.ToTable("TalepEkleri");
        e.Property(x => x.OrijinalAd).HasMaxLength(150).IsRequired();
        e.Property(x => x.DepoAdi).HasMaxLength(64).IsUnicode(false).IsRequired();
        e.HasIndex(x => x.DepoAdi).IsUnique();
        e.Property(x => x.IcerikTuru).HasMaxLength(50).IsUnicode(false).IsRequired();
    }
}

internal sealed class TalepHareketiYapilandirma : IEntityTypeConfiguration<TalepHareketi>
{
    public void Configure(EntityTypeBuilder<TalepHareketi> e)
    {
        e.ToTable("TalepHareketleri", t => t.HasTrigger("TR_TalepHareketleri_YalnizEkleme"));
        e.Property(x => x.EskiDurum).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.YeniDurum).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.MusteriNotu).HasMaxLength(1000);
        e.Property(x => x.IcNot).HasMaxLength(2000);
        e.HasIndex(x => new { x.TalepId, x.Zaman });
        e.HasOne(x => x.YapanKullanici).WithMany().HasForeignKey(x => x.YapanKullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AtamaKuraliYapilandirma : IEntityTypeConfiguration<AtamaKurali>
{
    public void Configure(EntityTypeBuilder<AtamaKurali> e)
    {
        e.ToTable("AtamaKurallari");
        e.Property(x => x.Konu).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => x.Oncelik);
        e.HasOne(x => x.Model).WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Sorumlu).WithMany().HasForeignKey(x => x.SorumluId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TalepSayaciYapilandirma : IEntityTypeConfiguration<TalepSayaci>
{
    public void Configure(EntityTypeBuilder<TalepSayaci> e)
    {
        e.ToTable("TalepSayaclari");
        e.HasKey(x => x.Yil);
        e.Property(x => x.Yil).ValueGeneratedNever();
    }
}

internal sealed class DurumSorguKilidiYapilandirma : IEntityTypeConfiguration<DurumSorguKilidi>
{
    public void Configure(EntityTypeBuilder<DurumSorguKilidi> e)
    {
        e.ToTable("DurumSorguKilitleri");
        e.HasKey(x => x.TalepNo);
        e.Property(x => x.TalepNo).HasMaxLength(TalepNumarasi.Uzunluk).IsFixedLength().IsUnicode(false);
    }
}

internal sealed class DenetimKaydiYapilandirma : IEntityTypeConfiguration<DenetimKaydi>
{
    public void Configure(EntityTypeBuilder<DenetimKaydi> e)
    {
        e.ToTable("DenetimKayitlari", t => t.HasTrigger("TR_DenetimKayitlari_YalnizEkleme"));
        e.Property(x => x.Islem).HasMaxLength(50).IsRequired();
        e.Property(x => x.Varlik).HasMaxLength(50).IsRequired();
        e.Property(x => x.VarlikNo).HasMaxLength(40);
        e.Property(x => x.Ayrinti).HasMaxLength(1000);
        e.HasIndex(x => x.Zaman);
        e.HasOne<Kullanici>().WithMany().HasForeignKey(x => x.KullaniciId).OnDelete(DeleteBehavior.Restrict);
    }
}
