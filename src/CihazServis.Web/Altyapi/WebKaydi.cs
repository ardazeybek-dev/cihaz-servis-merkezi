using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace CihazServis.Web.Altyapi;

public static class Politikalar
{
    public const string Personel = "Personel";
    public const string Musteri = "Musteri";
    public const string Yonetici = "Yonetici";

    public const string HizDogrulama = "Dogrulama";
    public const string HizDurumSorgusu = "DurumSorgusu";
    public const string HizForm = "Form";
    public const string HizSertifikaDogrulama = "SertifikaDogrulama";
}

public static class WebKaydi
{
    public static IServiceCollection AddWebKatmani(this IServiceCollection services, IConfiguration config)
    {
        services.AddRazorPages(o =>
        {
            o.Conventions.AuthorizeFolder("/Yonetim", Politikalar.Personel);
            o.Conventions.AuthorizeFolder("/Musteri", Politikalar.Musteri);
            o.Conventions.AuthorizeFolder("/Egitim");
        });
        services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Cihaz Servis Merkezi API";
            doc.Info.Description = "Garanti sorgusu, servis talebi durum sorgusu, eğitim izleme ve sertifika doğrulama uçları.";
            return Task.CompletedTask;
        }));
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));
        services.AddProblemDetails();
        // Türkçe harfler sayfada &#xE7; gibi kodlanmadan, olduğu gibi yazılır. HTML özel karakterleri yine kodlanır.
        services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
            o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));
        services.AddHttpContextAccessor();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/giris";
                o.LogoutPath = "/cikis";
                o.AccessDeniedPath = "/durum/403";
                o.Cookie.Name = "csm.oturum";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                // API isteklerinde giriş sayfasına yönlendirmek yerine 401/403 dönülür.
                o.Events.OnRedirectToLogin = ctx => ApiIseDurumKodu(ctx, StatusCodes.Status401Unauthorized);
                o.Events.OnRedirectToAccessDenied = ctx => ApiIseDurumKodu(ctx, StatusCodes.Status403Forbidden);
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(Politikalar.Personel, p => p.RequireRole("Personel", "Yonetici"))
            .AddPolicy(Politikalar.Musteri, p => p.RequireRole("Musteri"))
            .AddPolicy(Politikalar.Yonetici, p => p.RequireRole("Yonetici"));

        services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "csm.af";
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.HeaderName = "X-CSRF-TOKEN";
        });

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            foreach (var vekil in config.GetSection("Guvenlik:VekilSunucular").Get<string[]>() ?? [])
            {
                o.KnownProxies.Add(IPAddress.Parse(vekil));
            }
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = HizSiniriAsildi;
            foreach (var ad in new[] { Politikalar.HizDogrulama, Politikalar.HizDurumSorgusu, Politikalar.HizForm, Politikalar.HizSertifikaDogrulama })
            {
                var politika = ad;
                // Yalnız veri gönderen istekler sayılır, sayfanın kendisini açmak sınıra takılmaz.
                // Sertifika doğrulamada sorgunun kendisi GET olduğu için o politikada okuma da sayılır.
                var okumaSayilir = politika == Politikalar.HizSertifikaDogrulama;
                o.AddPolicy(politika, ctx => !okumaSayilir && (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method))
                    ? RateLimitPartition.GetNoLimiter("okuma")
                    : RateLimitPartition.GetFixedWindowLimiter(
                        ctx.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
                        _ =>
                        {
                            var bolum = ctx.RequestServices.GetRequiredService<IConfiguration>().GetSection($"HizSiniri:{politika}");
                            return new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = bolum.GetValue("Limit", 10),
                                Window = TimeSpan.FromSeconds(bolum.GetValue("PencereSaniye", 60)),
                                QueueLimit = 0,
                            };
                        }));
            }
        });

        return services;
    }

    private static Task ApiIseDurumKodu(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> ctx, int kod)
    {
        if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/medya"))
        {
            ctx.Response.StatusCode = kod;
            return Task.CompletedTask;
        }

        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }

    private static async ValueTask HizSiniriAsildi(OnRejectedContext ctx, CancellationToken ct)
    {
        var yanit = ctx.HttpContext.Response;
        var saniye = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var sure) ? (int)Math.Ceiling(sure.TotalSeconds) : 60;
        yanit.Headers.RetryAfter = saniye.ToString(System.Globalization.CultureInfo.InvariantCulture);
        yanit.Headers.CacheControl = "no-store";
        const string mesaj = "Kısa sürede çok fazla deneme yapıldı. Lütfen biraz bekleyip tekrar deneyin.";

        if (ctx.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            await yanit.WriteAsJsonAsync(new { hata = "cokFazlaIstek", mesaj, bekleSaniye = saniye }, ct);
            return;
        }

        yanit.ContentType = "text/html; charset=utf-8";
        await yanit.WriteAsync($"""
            <!doctype html><html lang="tr"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Çok fazla deneme</title><link rel="stylesheet" href="/css/site.css">
            <main class="kap dar"><h1>Biraz bekleyin</h1><p>{mesaj}</p><p>Yaklaşık {saniye} saniye sonra yeniden deneyebilirsiniz.</p>
            <p><a class="dugme" href="/">Ana sayfaya dön</a></p></main></html>
            """, ct);
    }

    public static int? KullaniciId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static int? KurumId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirstValue("kurum"), out var id) ? id : null;
}
