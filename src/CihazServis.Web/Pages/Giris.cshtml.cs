using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CihazServis.Web.Pages;

[EnableRateLimiting(Politikalar.HizForm)]
public class GirisModel(IUygulamaDbContext db, IPasswordHasher<Kullanici> hasher) : PageModel
{
    [BindProperty, Required(ErrorMessage = "E-posta adresini yazın."), EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi yazın."), Display(Name = "E-posta")]
    public string? Eposta { get; set; }

    [BindProperty, Required(ErrorMessage = "Şifreyi yazın."), DataType(DataType.Password), Display(Name = "Şifre")]
    public string? Sifre { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool Hatali { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var eposta = Eposta!.Trim().ToLowerInvariant();
        var k = await db.Kullanicilar.FirstOrDefaultAsync(x => x.Eposta == eposta, ct);
        if (k is null || hasher.VerifyHashedPassword(k, k.SifreOzeti, Sifre!) == PasswordVerificationResult.Failed)
        {
            // Hangi bilginin yanlış olduğu söylenmez: hesabın varlığı sızmasın.
            Hatali = true;
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, k.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, k.AdSoyad),
            new(ClaimTypes.Role, k.Rol.ToString()),
        };
        if (k.KurumId is { } kurum)
        {
            claims.Add(new Claim("kurum", kurum.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return Redirect(k.Rol == KullaniciRolu.Musteri ? "/egitim" : "/yonetim");
    }
}
