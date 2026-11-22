using CihazServis.Application.Servis;
using Hangfire;

namespace CihazServis.Infrastructure.Bildirim;

/// <summary>E-postalar Hangfire kuyruğuna girer: SMTP geçici olarak çalışmasa da iş yeniden denenir, istek beklemez.</summary>
public sealed class HangfireBildirimKuyrugu(IBackgroundJobClient isler) : IBildirimKuyrugu
{
    public void TalepAlindi(int talepId) => isler.Enqueue<ServisBildirimIsleri>(i => i.TalepAlindiAsync(talepId, CancellationToken.None));

    public void DurumDegisti(long hareketId) => isler.Enqueue<ServisBildirimIsleri>(i => i.DurumDegistiAsync(hareketId, CancellationToken.None));
}
