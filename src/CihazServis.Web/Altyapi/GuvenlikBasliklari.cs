namespace CihazServis.Web.Altyapi;

public static class GuvenlikBasliklari
{
    /// <summary>
    /// Uygulanan (rapor modu olmayan) içerik güvenliği politikası. Dış kaynak yok,
    /// satır içi betik yok. hls.js videoyu blob: adresinden oynatıp worker kullandığı
    /// için media-src ve worker-src blob: izni gerekir.
    /// </summary>
    public const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "media-src 'self' blob:; worker-src 'self' blob:; connect-src 'self'; font-src 'self'; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>
    /// Hangfire panosu satır içi stil kullanır. Yalnız yöneticiye açık bu yolda stil kuralı gevşetilir,
    /// betik kuralı aynı kalır.
    /// </summary>
    public static readonly string PanoCsp = Csp.Replace("style-src 'self'", "style-src 'self' 'unsafe-inline'", StringComparison.Ordinal);

    public static IApplicationBuilder UseGuvenlikBasliklari(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        // Yol istek başında okunur: Hangfire panosu yanıt başlamadan Request.Path'i değiştirir.
        var csp = ctx.Request.Path.StartsWithSegments(ArkaPlanKaydi.PanoYolu) ? PanoCsp : Csp;
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h.ContentSecurityPolicy = ctx.Request.IsHttps ? csp + "; upgrade-insecure-requests" : csp;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            return Task.CompletedTask;
        });
        await next();
    });
}
