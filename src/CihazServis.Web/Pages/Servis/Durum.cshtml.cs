using System.ComponentModel.DataAnnotations;
using CihazServis.Application.Ortak;
using CihazServis.Application.Servis;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace CihazServis.Web.Pages.Servis;

/// <summary>
/// Sorgu POST ile yapılır: talep numarası ve telefon haneleri adres satırına, tarayıcı geçmişine
/// ve sunucu günlüğüne düşmez.
/// </summary>
[EnableRateLimiting(Politikalar.HizDurumSorgusu)]
public class DurumModel(ServisTalebiServisi servis) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Talep numarasını yazın.")]
    [StringLength(30, ErrorMessage = "Talep numarasını SR-2026-000001 biçiminde yazın.")]
    [Display(Name = "Talep numarası")]
    public string? TalepNo { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Telefon numaranızın son 4 hanesini yazın.")]
    [StringLength(8, ErrorMessage = "Telefonun son 4 hanesini yazın.")]
    [Display(Name = "Telefonun son 4 hanesi")]
    public string? TelefonSon4 { get; set; }

    public TalepDurumYaniti? Yanit { get; private set; }

    public string? Hata { get; private set; }

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

        var sonuc = await servis.DurumSorgulaAsync(TalepNo, TelefonSon4, ct);
        if (sonuc.Basarili)
        {
            Yanit = sonuc.Deger;
        }
        else if (sonuc.Hata == HataTuru.Gecersiz)
        {
            ModelState.AddModelError(nameof(TalepNo), sonuc.Mesaj!);
        }
        else
        {
            Hata = sonuc.Mesaj;
        }

        TelefonSon4 = null;
        ModelState.Remove(nameof(TelefonSon4));
        return Page();
    }
}
