using CihazServis.Application.Dogrulama;
using CihazServis.Web.Altyapi;

namespace CihazServis.Web.Api;

public sealed record DogrulamaIstegi(string? SeriNo, string? Website);

public static partial class ApiUclari
{
    public static IEndpointRouteBuilder MapApiUclari(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1").DisableAntiforgery();

        v1.MapPost("/dogrulama", async (DogrulamaIstegi istek, DogrulamaServisi servis, HttpContext ctx, CancellationToken ct) =>
            {
                ctx.Response.Headers.CacheControl = "no-store";
                var yanit = await servis.SorgulaAsync(istek.SeriNo, istek.Website, ctx.Connection.RemoteIpAddress?.ToString(), ct);
                return Results.Ok(yanit);
            })
            .RequireRateLimiting(Politikalar.HizDogrulama)
            .WithName("CihazDogrula")
            .WithSummary("Seri numarasıyla cihaz doğrulama ve garanti sorgusu")
            .WithDescription("Yanıtta sahip, kurum ya da adres bilgisi yoktur. IP başına hız sınırlıdır, aşılırsa 429 döner.")
            .Produces<DogrulamaYaniti>()
            .Produces(StatusCodes.Status429TooManyRequests);

        MapEgitimUclari(v1);
        MapServisUclari(v1);
        return app;
    }

    static partial void MapEgitimUclari(RouteGroupBuilder v1);

    static partial void MapServisUclari(RouteGroupBuilder v1);
}
