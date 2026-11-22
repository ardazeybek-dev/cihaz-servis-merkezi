using System.Collections.Concurrent;
using CihazServis.Application.Servis;

namespace CihazServis.Tests.Altyapi;

public sealed class YakalayiciKuyruk : IBildirimKuyrugu
{
    public ConcurrentQueue<int> AlinanTalepler { get; } = new();

    public ConcurrentQueue<long> DurumDegisiklikleri { get; } = new();

    public void TalepAlindi(int talepId) => AlinanTalepler.Enqueue(talepId);

    public void DurumDegisti(long hareketId) => DurumDegisiklikleri.Enqueue(hareketId);
}
