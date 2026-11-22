using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Playwright;
using Testcontainers.MsSql;

namespace CihazServis.E2E;

/// <summary>
/// Uygulamayı gerçek Kestrel sunucusunda ayrı bir süreç olarak, kendi SQL Server konteyneriyle başlatır
/// ve testlere bir tarayıcı verir. Videoların önce scripts/medya-uret.sh ile üretilmiş olması gerekir.
/// </summary>
public sealed class UygulamaSureci : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Uygulamanın gönderdiği e-postaları yakalayan sahte SMTP sunucusu; HTTP API'siyle okunur.</summary>
    private readonly IContainer _smtp = new ContainerBuilder("rnwood/smtp4dev:v3")
        .WithPortBinding(25, true)
        .WithPortBinding(80, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/api/messages")))
        .Build();

    private readonly string _depo = Path.Combine(Path.GetTempPath(), "csm-e2e", Guid.NewGuid().ToString("N"));
    private Process? _surec;
    private IPlaywright? _pw;

    public string Adres { get; private set; } = "";
    public IBrowser Tarayici { get; private set; } = null!;

    /// <summary>smtp4dev API kökü, ör. http://127.0.0.1:49123.</summary>
    public string EpostaApi { get; private set; } = "";

    public static string DepoKoku
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "CihazServis.sln")))
            {
                d = d.Parent;
            }

            return d?.FullName ?? throw new InvalidOperationException("Depo kökü bulunamadı.");
        }
    }

    public async Task InitializeAsync()
    {
        var medya = Path.Combine(DepoKoku, "media");
        if (!File.Exists(Path.Combine(medya, "ders-1", "master.m3u8")))
        {
            throw new InvalidOperationException("Demo videoları yok. Önce scripts/medya-uret.sh çalıştırın.");
        }

        await Task.WhenAll(_sql.StartAsync(), _smtp.StartAsync());
        EpostaApi = $"http://{_smtp.Hostname}:{_smtp.GetMappedPublicPort(80)}";
        var port = BosPort();
        Adres = $"http://127.0.0.1:{port}";

        var bilgi = new ProcessStartInfo("dotnet", $"run --no-build --no-launch-profile --project \"{Path.Combine(DepoKoku, "src", "CihazServis.Web")}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var ortam = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = Adres,
            ["ConnectionStrings__Veritabani"] = _sql.GetConnectionString().Replace("Database=master", "Database=e2e", StringComparison.OrdinalIgnoreCase),
            ["Veritabani__OtomatikMigration"] = "true",
            ["Veritabani__DemoVerisi"] = "true",
            ["Hangfire__Etkin"] = "false",
            ["Medya__Klasor"] = medya,
            ["HizSiniri__Form__Limit"] = "1000",
            ["HizSiniri__Dogrulama__Limit"] = "1000",
            ["HizSiniri__DurumSorgusu__Limit"] = "1000",
            ["Eposta__Sunucu"] = _smtp.Hostname,
            ["Eposta__Port"] = _smtp.GetMappedPublicPort(25).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Eposta__Guvenlik"] = "None",
            ["Depolama__Klasor"] = _depo,
            ["Uygulama__GenelAdres"] = Adres,
        };
        foreach (var (k, v) in ortam)
        {
            bilgi.Environment[k] = v;
        }

        _surec = Process.Start(bilgi)!;
        _surec.OutputDataReceived += (_, _) => { };
        _surec.ErrorDataReceived += (_, _) => { };
        _surec.BeginOutputReadLine();
        _surec.BeginErrorReadLine();

        using var http = new HttpClient();
        for (var i = 0; i < 120; i++)
        {
            try
            {
                if ((await http.GetAsync($"{Adres}/saglik")).IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
            }

            if (_surec.HasExited)
            {
                throw new InvalidOperationException("Uygulama başlarken kapandı.");
            }

            await Task.Delay(1000);
        }

        _pw = await Playwright.CreateAsync();
        // Açık kaynak Chromium H.264 oynatamaz; kurulu Google Chrome kullanılır.
        Tarayici = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = Environment.GetEnvironmentVariable("E2E_TARAYICI") ?? "chrome",
            Headless = Environment.GetEnvironmentVariable("E2E_GORUNUR") != "1",
            Args = ["--autoplay-policy=no-user-gesture-required"],
        });
    }

    public async Task<IPage> SayfaAsync(string? giris = null, int genislik = 1280, int yukseklik = 860)
    {
        var baglam = await Tarayici.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Adres,
            ViewportSize = new ViewportSize { Width = genislik, Height = yukseklik },
            Locale = "tr-TR",
        });
        var sayfa = await baglam.NewPageAsync();
        if (giris is not null)
        {
            await sayfa.GotoAsync("/giris");
            await sayfa.FillAsync("#Eposta", giris);
            await sayfa.FillAsync("#Sifre", "Demo.2026!");
            await sayfa.ClickAsync("form button[type=submit]");
            await sayfa.WaitForURLAsync(u => !u.Contains("/giris", StringComparison.Ordinal));
        }

        return sayfa;
    }

    public async Task DisposeAsync()
    {
        if (Tarayici is not null)
        {
            await Tarayici.CloseAsync();
        }

        _pw?.Dispose();
        if (_surec is { HasExited: false })
        {
            _surec.Kill(entireProcessTree: true);
            await _surec.WaitForExitAsync();
        }

        await _sql.DisposeAsync();
        await _smtp.DisposeAsync();
        if (Directory.Exists(_depo))
        {
            Directory.Delete(_depo, recursive: true);
        }
    }

    private static int BosPort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }
}

[CollectionDefinition(Ad)]
public sealed class UygulamaKoleksiyonu : ICollectionFixture<UygulamaSureci>
{
    public const string Ad = "uygulama";
}
