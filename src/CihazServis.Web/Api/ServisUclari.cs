using CihazServis.Application.Ortak;
using CihazServis.Application.Servis;
using CihazServis.Web.Altyapi;

namespace CihazServis.Web.Api;

public sealed record DurumSorgusuIstegi(string? TalepNo, string? TelefonSon4);

public sealed record ApiHatasi(string Hata, string Mesaj);

public static partial class ApiUclari
{
    static partial void MapServisUclari(RouteGroupBuilder v1)
    {
        v1.MapPost("/servis/durum", async (DurumSorgusuIstegi istek, ServisTalebiServisi servis, HttpContext ctx, CancellationToken ct) =>
            {
                ctx.Response.Headers.CacheControl = "no-store";
                var s = await servis.DurumSorgulaAsync(istek.TalepNo, istek.TelefonSon4, ct);
                return s.Hata switch
                {
                    HataTuru.Yok => Results.Ok(s.Deger),
                    HataTuru.Gecersiz => Results.BadRequest(new ApiHatasi(s.Kod!, s.Mesaj!)),
                    HataTuru.Yasak => Results.Json(new ApiHatasi(s.Kod!, s.Mesaj!), statusCode: StatusCodes.Status429TooManyRequests),
                    _ => Results.NotFound(new ApiHatasi("eslesmedi", s.Mesaj!)),
                };
            })
            .RequireRateLimiting(Politikalar.HizDurumSorgusu)
            .WithName("TalepDurumu")
            .WithSummary("Talep numarası ve telefonun son 4 hanesiyle servis talebi durumu")
            .WithDescription("Yanıtta ad, telefon, e-posta, kurum ve iç not yoktur. Kayıt yoksa da hane yanlışsa da aynı 404 döner. "
                + "Aynı talep numarası için 5 hatalı denemede numara 15 dakika kilitlenir (429, hata: kilitli).")
            .Produces<TalepDurumYaniti>()
            .Produces<ApiHatasi>(StatusCodes.Status400BadRequest)
            .Produces<ApiHatasi>(StatusCodes.Status404NotFound)
            .Produces<ApiHatasi>(StatusCodes.Status429TooManyRequests);
    }
}
