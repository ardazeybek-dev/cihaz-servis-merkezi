namespace CihazServis.Application.Varliklar;

/// <summary>Garanti sorgusu günlüğü. Açık IP yerine anahtarlı özet tutulur.</summary>
public class DogrulamaSorgusu
{
    public long Id { get; set; }
    public DateTimeOffset Zaman { get; set; }
    public string IpOzeti { get; set; } = "";
    public string? SeriNo { get; set; }
    public string Sonuc { get; set; } = "";
    public bool TuzakDolu { get; set; }
}
