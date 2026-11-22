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

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
