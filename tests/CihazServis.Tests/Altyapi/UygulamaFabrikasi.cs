using System.Net;
using CihazServis.Infrastructure.Veri;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.MsSql;

namespace CihazServis.Tests.Altyapi;

/// <summary>Tüm entegrasyon testleri için tek SQL Server konteyneri.</summary>
public sealed class SqlKonteyner : IAsyncLifetime
{
    private readonly MsSqlContainer _konteyner = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _konteyner.StartAsync();

    public Task DisposeAsync() => _konteyner.DisposeAsync().AsTask();

    /// <summary>Her fabrika kendi veritabanını alır, testler birbirinin verisini görmez.</summary>
    public string YeniVeritabani() =>
        _konteyner.GetConnectionString().Replace("Database=master", $"Database=t_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase);
}

[CollectionDefinition(Ad)]
public sealed class SqlKoleksiyonu : ICollectionFixture<SqlKonteyner>
{
    public const string Ad = "sql";
}

public sealed class UygulamaFabrikasi(SqlKonteyner sql, IReadOnlyDictionary<string, string?>? ekAyarlar = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Testlerin ortak "bugün"ü. Demo verisindeki garanti tarihleri buna göre anlamlı.</summary>
    public static readonly DateTimeOffset Baslangic = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _baglanti = sql.YeniVeritabani();

    public FakeTimeProvider Saat { get; } = new(Baslangic);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        var ayarlar = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Veritabani"] = _baglanti,
            ["Veritabani:OtomatikMigration"] = "true",
            ["Veritabani:DemoVerisi"] = "true",
            ["Guvenlik:IpOzetAnahtari"] = "test-ip-ozet-anahtari-0123456789",
            ["Guvenlik:MedyaImzaAnahtari"] = "test-medya-imza-anahtari-0123456789",
            ["Hangfire:Etkin"] = "false",
            ["HizSiniri:Dogrulama:Limit"] = "1000",
            ["HizSiniri:DurumSorgusu:Limit"] = "1000",
            ["HizSiniri:Form:Limit"] = "1000",
            ["HizSiniri:SertifikaDogrulama:Limit"] = "1000",
        };
        foreach (var (k, v) in ekAyarlar ?? new Dictionary<string, string?>())
        {
            ayarlar[k] = v;
        }

        foreach (var (k, v) in ayarlar)
        {
            builder.UseSetting(k, v);
        }

        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Saat);
            s.AddSingleton<IStartupFilter, TestIpFiltresi>();
        });
    }

    public HttpClient Istemci(string? ip = null, bool yonlendirmeyiIzle = false)
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = yonlendirmeyiIzle,
            HandleCookies = true,
        });
        if (ip is not null)
        {
            c.DefaultRequestHeaders.Add(TestIpFiltresi.Baslik, ip);
        }

        return c;
    }

    public async Task<T> VeriAsync<T>(Func<UygulamaDbContext, Task<T>> islem)
    {
        await using var scope = Services.CreateAsyncScope();
        return await islem(scope.ServiceProvider.GetRequiredService<UygulamaDbContext>());
    }
}

/// <summary>TestServer'da uzak IP yoktur. Testler hız sınırını IP başına denemek için başlıkla IP verir.</summary>
internal sealed class TestIpFiltresi : IStartupFilter
{
    public const string Baslik = "X-Test-IP";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((HttpContext ctx, RequestDelegate sonraki) =>
        {
            ctx.Connection.RemoteIpAddress = ctx.Request.Headers.TryGetValue(Baslik, out var ip)
                ? IPAddress.Parse(ip.ToString())
                : IPAddress.Loopback;
            return sonraki(ctx);
        });
        next(app);
    };
}
