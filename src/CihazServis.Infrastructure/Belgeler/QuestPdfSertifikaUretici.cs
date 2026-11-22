using System.Globalization;
using CihazServis.Application.Egitim;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CihazServis.Infrastructure.Belgeler;

public sealed class QuestPdfSertifikaUretici : ISertifikaPdfUretici
{
    private const string Font = "Noto Sans";
    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly Lazy<bool> Hazir = new(() =>
    {
        // Community lisansı: yıllık cirosu 1 milyon USD altındaki kuruluşlar için ücretsiz.
        QuestPDF.Settings.License = LicenseType.Community;
        // Fontlar gömülüdür: fontu olmayan Linux sunucuda da ş, ğ, İ doğru basılır.
        var asm = typeof(QuestPdfSertifikaUretici).Assembly;
        foreach (var ad in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.Ordinal)))
        {
            using var akis = asm.GetManifestResourceStream(ad)!;
            FontManager.RegisterFontFromStream(akis);
        }

        return true;
    });

    public byte[] Uret(SertifikaPdfVerisi v, string dogrulamaAdresi)
    {
        _ = Hazir.Value;
        var karekod = Karekod(dogrulamaAdresi);
        const string vurgu = "#0D5C63";

        return Document.Create(doc => doc.Page(p =>
        {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(36);
            p.PageColor(Colors.White);
            p.DefaultTextStyle(t => t.FontFamily(Font).FontSize(11).FontColor("#1B1D1F"));

            p.Content().Border(1.5f).BorderColor(vurgu).Padding(40).Column(c =>
            {
                c.Spacing(6);
                c.Item().Text("CİHAZ SERVİS MERKEZİ").FontSize(10).Bold().FontColor(vurgu).LetterSpacing(0.15f);
                c.Item().PaddingTop(28).Text("Kullanım Sertifikası").FontSize(34).Bold();
                c.Item().PaddingTop(18).Text("Bu belge").FontSize(12).FontColor("#5B6066");
                c.Item().Text(v.AdSoyad).FontSize(26).Bold();
                c.Item().PaddingTop(6).Text(t =>
                {
                    t.Span("adlı kullanıcının ").FontColor("#5B6066");
                    t.Span(v.KursAdi).Bold();
                    t.Span(" kapsamındaki tüm dersleri izleyip soruları başarıyla yanıtladığını gösterir.").FontColor("#5B6066");
                });

                c.Item().PaddingTop(36).Row(r =>
                {
                    r.RelativeItem().Column(b =>
                    {
                        b.Spacing(4);
                        Satir(b, "Cihaz modeli", v.ModelAdi ?? "-");
                        Satir(b, "Veriliş tarihi", v.VerilisZamani.ToString("d MMMM yyyy", Tr));
                        Satir(b, "Sertifika no", v.No);
                        b.Item().PaddingTop(10).Text("Geçerliliği karekodu okutarak ya da aşağıdaki adresten doğrulayabilirsiniz.")
                            .FontSize(9).FontColor("#5B6066");
                        b.Item().Text(dogrulamaAdresi).FontSize(9).FontColor(vurgu);
                    });
                    r.ConstantItem(130).AlignRight().AlignBottom().Width(120).Height(120).Image(karekod);
                });

                c.Item().ExtendVertical().AlignBottom().Text("Örnek uygulamadır, gerçek bir sertifika değildir.")
                    .FontSize(8).FontColor("#9A9EA3");
            });
        })).GeneratePdf();
    }

    private static void Satir(ColumnDescriptor b, string etiket, string deger) => b.Item().Text(t =>
    {
        t.Span(etiket + "  ").FontSize(9).FontColor("#5B6066");
        t.Span(deger).FontSize(11).SemiBold();
    });

    public static byte[] Karekod(string metin)
    {
        using var uretici = new QRCodeGenerator();
        using var veri = uretici.CreateQrCode(metin, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(veri).GetGraphic(12);
    }
}
