using System.Text;
using System.Text.RegularExpressions;
using CihazServis.Application.Egitim;
using CihazServis.Application.Ortak;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Antiforgery;

namespace CihazServis.Web.Api;

public sealed record QuizCevabi(Dictionary<int, int> Cevaplar);

public static partial class ApiUclari
{
    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex MedyaAnahtariDeseni();

    [GeneratedRegex(@"^[A-Za-z0-9_\-]+(/[A-Za-z0-9_\-]+)*\.(m3u8|ts|m4s|mp4|vtt)$")]
    private static partial Regex MedyaYoluDeseni();

    [GeneratedRegex("URI=\"([^\"]+)\"")]
    private static partial Regex PlaylistUriDeseni();

    static partial void MapEgitimUclari(RouteGroupBuilder v1)
    {
        var egitim = v1.MapGroup("/egitim")
            .RequireAuthorization()
            .AddEndpointFilter(AntiforgeryDogrula)
            .WithTags("Eğitim");

        egitim.MapPost("/dersler/{dersId:int}/oturum", async (int dersId, EgitimServisi s, HttpContext ctx, CancellationToken ct) =>
                Sonucla(await s.OturumBaslatAsync(ctx.User.KullaniciId()!.Value, dersId, ct)))
            .WithSummary("Oynatma oturumu açar, süreli imzalı oynatma adresini döner. Önceki oturum geçersiz olur.");

        egitim.MapPost("/dersler/{dersId:int}/izleme", async (int dersId, IzlemeBildirimi b, EgitimServisi s, HttpContext ctx, CancellationToken ct) =>
                Sonucla(await s.IzlemeBildirAsync(ctx.User.KullaniciId()!.Value, dersId, b, ct)))
            .WithSummary("İzlenen aralığı bildirir. İleri atlama ve gerçek süreden hızlı izleme 422 ile reddedilir.");

        egitim.MapPost("/dersler/{dersId:int}/quiz", async (int dersId, QuizServisi s, HttpContext ctx, CancellationToken ct) =>
                Sonucla(await s.BaslatAsync(ctx.User.KullaniciId()!.Value, dersId, ct)))
            .WithSummary("Quiz denemesini başlatır ya da yarıda kalanı döner. Yanıtta doğru cevap bilgisi yoktur.");

        egitim.MapPost("/quiz/{denemeId:guid}/cevap", async (Guid denemeId, QuizCevabi c, QuizServisi s, HttpContext ctx, CancellationToken ct) =>
                Sonucla(await s.CevaplaAsync(ctx.User.KullaniciId()!.Value, denemeId, c.Cevaplar, ct)))
            .WithSummary("Cevapları gönderir, puan sunucuda hesaplanır.");

        v1.MapGet("/sertifikalar/{no}", async (string no, SertifikaServisi s, HttpContext ctx, CancellationToken ct) =>
            {
                ctx.Response.Headers.CacheControl = "no-store";
                var d = await s.DogrulaAsync(no, ct);
                return d is null ? Results.NotFound(new { hata = "bulunamadi", mesaj = "Bu numarayla bir sertifika yok." }) : Results.Ok(d);
            })
            .RequireRateLimiting(Politikalar.HizSertifikaDogrulama)
            .WithTags("Eğitim")
            .WithSummary("Herkese açık sertifika doğrulama. Tam ad dönmez, yalnız baş harfler.");
    }

    public static IEndpointRouteBuilder MapEgitimDosyaUclari(this IEndpointRouteBuilder app)
    {
        app.MapGet("/egitim/sertifikalar/{no}/pdf", async (string no, SertifikaServisi s, ISertifikaPdfUretici pdf,
                IConfiguration config, HttpContext ctx, CancellationToken ct) =>
            {
                var v = await s.PdfVerisiAsync(ctx.User.KullaniciId()!.Value, no, ct);
                if (v is null)
                {
                    return Results.NotFound();
                }

                var kok = config["Uygulama:GenelAdres"] is { Length: > 0 } a ? a.TrimEnd('/') : $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                ctx.Response.Headers.CacheControl = "private, no-store";
                return Results.File(pdf.Uret(v, $"{kok}/sertifika/{v.No}"), "application/pdf", $"sertifika-{v.No}.pdf");
            })
            .RequireAuthorization()
            .ExcludeFromDescription();

        app.MapGet("/medya/{anahtar}/{**yol}", MedyaVer).RequireAuthorization().ExcludeFromDescription();
        return app;
    }

    /// <summary>
    /// Web kökü dışındaki HLS dosyalarını yalnız geçerli imzayla verir. Playlist'ler yeniden yazılır:
    /// içindeki her alt playlist, segment ve altyazı adresine aynı imza eklenir, yoksa segmentler açılmaz.
    /// </summary>
    private static async Task<IResult> MedyaVer(string anahtar, string yol, int? u, long? e, string? s,
        HttpContext ctx, IMedyaImzalayici imzalayici, IConfiguration config, IWebHostEnvironment env, CancellationToken ct)
    {
        var kullanici = ctx.User.KullaniciId();
        if (u is null || e is null || s is null || kullanici != u
            || !MedyaAnahtariDeseni().IsMatch(anahtar) || !imzalayici.Dogrula(anahtar, u.Value, e.Value, s))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!MedyaYoluDeseni().IsMatch(yol))
        {
            return Results.NotFound();
        }

        var kok = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Medya:Klasor"] ?? "media"));
        var dosya = Path.GetFullPath(Path.Combine(kok, anahtar, yol));
        if (!dosya.StartsWith(kok + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(dosya))
        {
            return Results.NotFound();
        }

        var uzanti = Path.GetExtension(dosya);
        if (uzanti == ".m3u8")
        {
            ctx.Response.Headers.CacheControl = "private, no-store";
            var sorgu = ctx.Request.QueryString.Value!.TrimStart('?');
            var icerik = PlaylistYenidenYaz(await File.ReadAllTextAsync(dosya, ct), sorgu);
            return Results.Text(icerik, "application/vnd.apple.mpegurl", Encoding.UTF8);
        }

        ctx.Response.Headers.CacheControl = "private, max-age=600";
        var tur = uzanti switch
        {
            ".ts" => "video/mp2t",
            ".m4s" => "video/iso.segment",
            ".mp4" => "video/mp4",
            _ => "text/vtt; charset=utf-8",
        };
        return Results.File(dosya, tur, enableRangeProcessing: true);
    }

    public static string PlaylistYenidenYaz(string icerik, string sorgu)
    {
        static string Ekle(string uri, string q) => uri.Contains("://", StringComparison.Ordinal) ? uri : uri + (uri.Contains('?') ? "&" : "?") + q;

        var sb = new StringBuilder(icerik.Length + 256);
        foreach (var hamSatir in icerik.Split('\n'))
        {
            var satir = hamSatir.TrimEnd('\r');
            if (satir.Length > 0)
            {
                satir = satir.StartsWith('#')
                    ? PlaylistUriDeseni().Replace(satir, m => $"URI=\"{Ekle(m.Groups[1].Value, sorgu)}\"")
                    : Ekle(satir, sorgu);
            }

            sb.Append(satir).Append('\n');
        }

        return sb.ToString().TrimEnd('\n') + "\n";
    }

    private static async ValueTask<object?> AntiforgeryDogrula(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        // Çerezle oturum açılan uçlar: başka bir siteden tetiklenemesin diye istek anahtarı zorunlu.
        var af = ctx.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await af.IsRequestValidAsync(ctx.HttpContext))
        {
            return Results.Json(new { hata = "gecersizIstekAnahtari", mesaj = "Sayfayı yenileyip tekrar deneyin." }, statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(ctx);
    }

    private static IResult Sonucla<T>(Sonuc<T> s) => s.Hata switch
    {
        HataTuru.Yok => Results.Ok(s.Deger),
        HataTuru.Bulunamadi => Results.NotFound(new { hata = s.Kod, mesaj = s.Mesaj }),
        HataTuru.Yasak => Results.Json(new { hata = s.Kod, mesaj = s.Mesaj }, statusCode: StatusCodes.Status403Forbidden),
        HataTuru.Cakisma => Results.Conflict(new { hata = s.Kod, mesaj = s.Mesaj }),
        _ => Results.UnprocessableEntity(new { hata = s.Kod, mesaj = s.Mesaj, durum = s.Deger }),
    };
}
