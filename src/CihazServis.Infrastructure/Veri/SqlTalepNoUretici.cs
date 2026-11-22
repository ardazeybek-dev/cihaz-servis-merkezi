using System.Data;
using CihazServis.Application.Servis;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Infrastructure.Veri;

/// <summary>
/// Yıl satırındaki sayaç tek UPDATE cümlesiyle artırılır ve yeni değer aynı cümlede okunur,
/// iki istek aynı numarayı alamaz. Yılın ilk talebinde satır eklenir; aynı anda iki ekleme
/// olursa birincil anahtar çakışan isteği güncellemeye düşürür. Kaydı başarısız olan talep
/// numarada boşluk bırakabilir, numara tekrar etmez.
/// </summary>
public sealed class SqlTalepNoUretici(UygulamaDbContext db) : ITalepNoUretici
{
    private const string Sql = $"""
        SET NOCOUNT ON;
        UPDATE [{UygulamaDbContext.Sema}].[TalepSayaclari] SET @son = [Son] = [Son] + 1 WHERE [Yil] = @yil;
        IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO [{UygulamaDbContext.Sema}].[TalepSayaclari] ([Yil], [Son]) VALUES (@yil, 1);
                SET @son = 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
                UPDATE [{UygulamaDbContext.Sema}].[TalepSayaclari] SET @son = [Son] = [Son] + 1 WHERE [Yil] = @yil;
            END CATCH
        END
        """;

    public async Task<string> SonrakiAsync(int yil, CancellationToken ct = default)
    {
        var son = new SqlParameter("@son", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(Sql, [new SqlParameter("@yil", yil), son], ct);
        return TalepNumarasi.Olustur(yil, (int)son.Value);
    }
}
