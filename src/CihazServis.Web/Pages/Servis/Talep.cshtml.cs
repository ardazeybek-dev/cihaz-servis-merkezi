using System.ComponentModel.DataAnnotations;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace CihazServis.Web.Pages.Servis;

[EnableRateLimiting(Politikalar.HizForm)]
[RequestSizeLimit(ServisTalebiServisi.AzamiToplamEkBoyutu + (2 * 1024 * 1024))] // web.config'te maxAllowedContentLength da en az bu kadar olmalı
public class TalepModel(ServisTalebiServisi servis) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [StringLength(40, ErrorMessage = "Seri numarası en fazla 40 karakter olabilir.")]
    [Display(Name = "Seri numarası")]
    public string? SeriNo { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Talep konusunu seçin.")]
    [Display(Name = "Konu")]
    public TalepKonusu? Konu { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Sorunu kısaca anlatın.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Açıklama 10 ile 2000 karakter arasında olmalı.")]
    [Display(Name = "Açıklama")]
    public string? Aciklama { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Adınızı ve soyadınızı yazın.")]
    [StringLength(150, ErrorMessage = "Ad soyad en fazla 150 karakter olabilir.")]
    [Display(Name = "Ad soyad")]
    public string? AdSoyad { get; set; }

    [BindProperty]
    [StringLength(200, ErrorMessage = "Kurum adı en fazla 200 karakter olabilir.")]
    [Display(Name = "Kurum (isteğe bağlı)")]
    public string? KurumAdi { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Telefon numaranızı yazın.")]
    [StringLength(25, ErrorMessage = "Telefon numarasını 0555 123 45 67 biçiminde yazın.")]
    [Display(Name = "Telefon")]
    public string? Telefon { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "E-posta adresinizi yazın.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın.")]
    [StringLength(254)]
    [Display(Name = "E-posta")]
    public string? Eposta { get; set; }

    [BindProperty]
    [Display(Name = "Fotoğraf, video ya da belge (isteğe bağlı)")]
    public List<IFormFile> Ekler { get; set; } = [];

    /// <summary>Onay kutusu işaretlenmezse false gelir; [Required] bunu yakalamaz, elle denetlenir.</summary>
    [BindProperty]
    public bool AydinlatmaOnay { get; set; }

    /// <summary>Kullanıcının gördüğü metnin sürümü. Bu arada metin değiştiyse talep reddedilir.</summary>
    [BindProperty]
    public string? AydinlatmaSurumu { get; set; }

    /// <summary>Tuzak alan: ekranda görünmez, yalnız botlar doldurur.</summary>
    [BindProperty]
    public string? Website { get; set; }

    public void OnGet()
    {
        ModelState.Clear();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrEmpty(Website) && !AydinlatmaOnay)
        {
            ModelState.AddModelError(nameof(AydinlatmaOnay), "Devam etmek için aydınlatma metnini okuduğunuzu onaylayın.");
        }

        if (string.IsNullOrEmpty(Website) && !ModelState.IsValid)
        {
            return Page();
        }

        var girdi = new TalepGirdisi(SeriNo, Konu ?? TalepKonusu.Diger, Aciklama ?? "", AdSoyad ?? "", KurumAdi, Telefon ?? "", Eposta ?? "",
            AydinlatmaSurumu, AydinlatmaOnay, Website, HttpContext.Connection.RemoteIpAddress?.ToString());
        var ekler = Ekler.Where(e => e.Length > 0 || !string.IsNullOrEmpty(e.FileName))
            .Select(e => new EkGirdisi(e.FileName, e.Length, e.OpenReadStream)).ToList();
        var sonuc = await servis.OlusturAsync(girdi, ekler, ct);
        if (!sonuc.Basarili)
        {
            var alan = sonuc.Kod switch
            {
                "telefon" => nameof(Telefon),
                "aydinlatma" or "aydinlatmaSurumu" => nameof(AydinlatmaOnay),
                _ => nameof(Ekler),
            };
            ModelState.AddModelError(alan, sonuc.Mesaj!);
            return Page();
        }

        TempData["TalepNo"] = sonuc.Deger!.No ?? "";
        TempData["HedefTarih"] = sonuc.Deger.HedefTarih?.ToString("O");
        return RedirectToPage("/Servis/Alindi");
    }
}
