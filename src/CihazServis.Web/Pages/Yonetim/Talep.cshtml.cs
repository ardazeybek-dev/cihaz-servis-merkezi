using System.ComponentModel.DataAnnotations;
using CihazServis.Application.Ortak;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Yonetim;

/// <summary>/Yonetim klasörü konvansiyonla Personel politikasına bağlı: ek indirme de bu sayfanın işleyicisi.</summary>
public class TalepModel(ServisTalebiServisi servis) : PageModel
{
    [FromRoute]
    public string No { get; set; } = "";

    public ServisTalebi? Talep { get; private set; }

    [BindProperty]
    [Display(Name = "Yeni durum")]
    public TalepDurumu YeniDurum { get; set; }

    /// <summary>Formu açtığında talebin durumu. Arada başkası değiştirdiyse işlem reddedilir.</summary>
    [BindProperty]
    public TalepDurumu BeklenenDurum { get; set; }

    [BindProperty]
    [StringLength(1000, ErrorMessage = "Müşteri notu en fazla 1000 karakter olabilir.")]
    [Display(Name = "Müşteriye görünen not")]
    public string? MusteriNotu { get; set; }

    [BindProperty]
    [StringLength(2000, ErrorMessage = "İç not en fazla 2000 karakter olabilir.")]
    [Display(Name = "İç not (yalnız ekip görür)")]
    public string? IcNot { get; set; }

    [TempData]
    public string? Bildirim { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Talep = await servis.DetayAsync(No, ct);
        if (Talep is null)
        {
            return NotFound();
        }

        YeniDurum = Talep.Durum;
        BeklenenDurum = Talep.Durum;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var s = await servis.DurumDegistirAsync(No, YeniDurum, BeklenenDurum, MusteriNotu, IcNot, User.KullaniciId()!.Value, ct);
            if (s.Hata == HataTuru.Bulunamadi)
            {
                return NotFound();
            }

            if (s.Basarili)
            {
                Bildirim = YeniDurum == BeklenenDurum ? "Not eklendi." : $"Durum {TalepDurumAkisi.Ad(YeniDurum)} olarak güncellendi.";
                return RedirectToPage(new { No });
            }

            ModelState.AddModelError(nameof(YeniDurum), s.Mesaj!);
        }

        Talep = await servis.DetayAsync(No, ct);
        if (Talep is null)
        {
            return NotFound();
        }

        BeklenenDurum = Talep.Durum;
        return Page();
    }

    public async Task<IActionResult> OnGetEkAsync(int ekId, CancellationToken ct)
    {
        var ek = await servis.EkAcAsync(No, ekId, User.KullaniciId()!.Value, ct);
        if (ek is null)
        {
            return NotFound();
        }

        // Tür dosyanın kendi imzasından gelir; tarayıcı içeriği çalıştırmaz, indirir.
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return File(ek.Akis, ek.IcerikTuru, ek.Ad);
    }
}
