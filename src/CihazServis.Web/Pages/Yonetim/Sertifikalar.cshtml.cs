using System.ComponentModel.DataAnnotations;
using CihazServis.Application.Egitim;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Yonetim;

public class SertifikalarModel(SertifikaServisi servis) : PageModel
{
    public List<SertifikaServisi.SertifikaSatiri> Liste { get; private set; } = [];

    [BindProperty]
    public string? No { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "İptal gerekçesini yazın.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Gerekçe 5 ile 500 karakter arasında olmalı.")]
    public string? Gerekce { get; set; }

    [TempData]
    public string? Bildirim { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Liste = await servis.ListeleAsync(ct);
    }

    public async Task<IActionResult> OnPostIptalAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var s = await servis.IptalEtAsync(No ?? "", User.KullaniciId()!.Value, Gerekce!, ct);
            if (s.Basarili)
            {
                Bildirim = $"{No} numaralı sertifika iptal edildi. Doğrulama sayfası artık geçersiz gösterir.";
                return RedirectToPage();
            }

            ModelState.AddModelError(nameof(Gerekce), s.Mesaj!);
        }

        Liste = await servis.ListeleAsync(ct);
        return Page();
    }
}
