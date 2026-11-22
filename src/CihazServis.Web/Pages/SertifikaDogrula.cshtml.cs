using CihazServis.Application.Egitim;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace CihazServis.Web.Pages;

[EnableRateLimiting(Politikalar.HizSertifikaDogrulama)]
public class SertifikaDogrulaModel(SertifikaServisi sertifika) : PageModel
{
    public SertifikaDogrulamaDto? Sonuc { get; private set; }
    public string? No { get; private set; }

    public async Task OnGetAsync(string? no, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        No = no;
        Sonuc = await sertifika.DogrulaAsync(no, ct);
        if (Sonuc is null && no is not null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
}
