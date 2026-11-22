using System.ComponentModel.DataAnnotations;
using CihazServis.Application.Dogrulama;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace CihazServis.Web.Pages;

[EnableRateLimiting(Politikalar.HizDogrulama)]
public class GarantiModel(DogrulamaServisi servis) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Seri numarasını yazın.")]
    [StringLength(40, ErrorMessage = "Seri numarası en fazla 40 karakter olabilir.")]
    [Display(Name = "Seri numarası")]
    public string? SeriNo { get; set; }

    /// <summary>Tuzak alan: ekranda görünmez, yalnız botlar doldurur.</summary>
    [BindProperty]
    public string? Website { get; set; }

    public DogrulamaYaniti? Yanit { get; private set; }

    public void OnGet()
    {
        ModelState.Clear();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Yanit = await servis.SorgulaAsync(SeriNo, Website, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return Page();
    }
}
