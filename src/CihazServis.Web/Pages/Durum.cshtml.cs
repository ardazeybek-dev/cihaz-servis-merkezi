using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages;

/// <summary>Hata sayfası, form gönderiminden sonra da (POST) yeniden çalışabilir.</summary>
[IgnoreAntiforgeryToken]
public class DurumModel : PageModel
{
    public int Kod { get; private set; }

    public void OnGet(int kod) => Kod = kod;

    public void OnPost(int kod) => Kod = kod;
}
