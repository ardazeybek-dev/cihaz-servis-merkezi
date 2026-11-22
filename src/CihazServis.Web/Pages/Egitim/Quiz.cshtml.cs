using CihazServis.Application.Egitim;
using CihazServis.Application.Ortak;
using CihazServis.Web.Altyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CihazServis.Web.Pages.Egitim;

public class QuizModel(QuizServisi quiz) : PageModel
{
    public QuizDto? Quiz { get; private set; }
    public QuizSonucu? Sonuc { get; private set; }
    public string? Uyari { get; private set; }
    public int DersId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int dersId, CancellationToken ct)
    {
        DersId = dersId;
        var s = await quiz.BaslatAsync(User.KullaniciId()!.Value, dersId, ct);
        switch (s.Hata)
        {
            case HataTuru.Yok:
                Quiz = s.Deger;
                return Page();
            case HataTuru.Bulunamadi:
                return NotFound();
            case HataTuru.Yasak:
                return StatusCode(StatusCodes.Status403Forbidden);
            default:
                return Redirect("/egitim");
        }
    }

    public async Task<IActionResult> OnPostAsync(int dersId, Guid denemeId, CancellationToken ct)
    {
        DersId = dersId;
        var cevaplar = new Dictionary<int, int>();
        foreach (var (anahtar, deger) in Request.Form)
        {
            if (anahtar.StartsWith("soru-", StringComparison.Ordinal)
                && int.TryParse(anahtar.AsSpan(5), out var soru) && int.TryParse(deger, out var secenek))
            {
                cevaplar[soru] = secenek;
            }
        }

        var s = await quiz.CevaplaAsync(User.KullaniciId()!.Value, denemeId, cevaplar, ct);
        if (s.Hata == HataTuru.Bulunamadi)
        {
            return NotFound();
        }

        if (!s.Basarili)
        {
            Uyari = s.Mesaj;
            return Page();
        }

        Sonuc = s.Deger;
        return Page();
    }
}
