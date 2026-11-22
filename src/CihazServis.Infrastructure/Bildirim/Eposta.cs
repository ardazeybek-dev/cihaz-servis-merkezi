using System.Globalization;
using CihazServis.Application.Servis;
using CihazServis.Application.Varliklar;
using CihazServis.Infrastructure.Veri;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CihazServis.Infrastructure.Bildirim;

public sealed class MailKitEpostaGonderici(IConfiguration config) : IEpostaGonderici
{
    public async Task GonderAsync(EpostaMesaji mesaj, CancellationToken ct = default)
    {
        var a = config.GetSection("Eposta");
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(a["GonderenAd"] ?? "Cihaz Servis Merkezi", a["Gonderen"] ?? "servis@ornek.test"));
        m.To.Add(new MailboxAddress(mesaj.AliciAd, mesaj.AliciEposta));
        m.Subject = mesaj.Konu;
        m.Body = new TextPart("plain") { Text = mesaj.Metin };

        using var smtp = new SmtpClient();
        var guvenlik = Enum.TryParse<SecureSocketOptions>(a["Guvenlik"], true, out var g) ? g : SecureSocketOptions.Auto;
        await smtp.ConnectAsync(a["Sunucu"] ?? "localhost", a.GetValue("Port", 25), guvenlik, ct);
        if (a["Kullanici"] is { Length: > 0 } kullanici)
        {
            await smtp.AuthenticateAsync(kullanici, a["Sifre"] ?? "", ct);
        }

        await smtp.SendAsync(m, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}

/// <summary>
/// Kuyruktan çalışan e-posta işleri. Yalnız kimlik alır, veriyi çalışma anında okur:
/// kuyrukta kişisel veri birikmez. İç not hiçbir e-postaya girmez.
/// </summary>
public sealed class ServisBildirimIsleri(UygulamaDbContext db, IEpostaGonderici gonderici, IConfiguration config)
{
    private static readonly CultureInfo Tr = new("tr-TR");

    public async Task TalepAlindiAsync(int talepId, CancellationToken ct)
    {
        var t = await db.ServisTalepleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == talepId, ct);
        if (t is null)
        {
            return;
        }

        await gonderici.GonderAsync(new EpostaMesaji(t.Eposta, t.AdSoyad, $"Servis talebiniz alındı: {t.No}", $"""
            Merhaba {t.AdSoyad},

            {TalepKonusuBilgi.Ad(t.Konu)} konulu servis talebiniz alındı.

            Talep numarası: {t.No}
            Tahmini tamamlanma: {t.HedefTarih.ToString("d MMMM yyyy dddd", Tr)}

            Talebin durumunu talep numarası ve telefon numaranızın son 4 hanesiyle buradan takip edebilirsiniz:
            {DurumAdresi()}

            Cihaz Servis Merkezi
            """), ct);
    }

    public async Task DurumDegistiAsync(long hareketId, CancellationToken ct)
    {
        var h = await db.TalepHareketleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == hareketId, ct);
        var t = h is null ? null : await db.ServisTalepleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == h.TalepId, ct);
        if (h is null || t is null)
        {
            return;
        }

        var not = h.MusteriNotu is null ? "" : $"\nServis ekibinin notu: {h.MusteriNotu}\n";
        var baslik = h.EskiDurum == h.YeniDurum ? "Talebinize not eklendi" : $"Talebinizin durumu: {TalepDurumAkisi.Ad(h.YeniDurum)}";
        await gonderici.GonderAsync(new EpostaMesaji(t.Eposta, t.AdSoyad, $"{baslik} ({t.No})", $"""
            Merhaba {t.AdSoyad},

            {t.No} numaralı servis talebinizin güncel durumu: {TalepDurumAkisi.Ad(h.YeniDurum)}.
            {not}
            {TalepDurumAkisi.SiradakiAdim(h.YeniDurum)}

            Ayrıntılar ve zaman çizelgesi:
            {DurumAdresi()}

            Cihaz Servis Merkezi
            """), ct);
    }

    private string DurumAdresi()
        => (config["Uygulama:GenelAdres"] is { Length: > 0 } a ? a.TrimEnd('/') : "http://localhost:5080") + "/servis/durum";
}

/// <summary>Hangfire kapalıyken e-postayı istekten bağımsız, hemen gönderir. Hata isteği bozmaz, günlüğe yazılır.</summary>
public sealed class AnindaBildirimKuyrugu(IServiceScopeFactory kapsam, ILogger<AnindaBildirimKuyrugu> log) : IBildirimKuyrugu
{
    public void TalepAlindi(int talepId) => Calistir(i => i.TalepAlindiAsync(talepId, CancellationToken.None));

    public void DurumDegisti(long hareketId) => Calistir(i => i.DurumDegistiAsync(hareketId, CancellationToken.None));

    private void Calistir(Func<ServisBildirimIsleri, Task> is_) => _ = Task.Run(async () =>
    {
        try
        {
            await using var s = kapsam.CreateAsyncScope();
            await is_(s.ServiceProvider.GetRequiredService<ServisBildirimIsleri>());
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Bildirim e-postası gönderilemedi");
        }
    });
}
