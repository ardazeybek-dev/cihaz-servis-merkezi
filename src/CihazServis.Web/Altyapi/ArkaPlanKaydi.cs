using CihazServis.Application.Servis;
using CihazServis.Infrastructure.Bildirim;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;

namespace CihazServis.Web.Altyapi;

/// <summary>
/// Hangfire yalnız <c>Hangfire:Etkin</c> açıkken kurulur. Kapalıyken e-posta istekten bağımsız
/// hemen gönderilir; testler kuyruğu kendi yakalayıcısıyla değiştirir.
/// </summary>
public static class ArkaPlanKaydi
{
    public const string PanoYolu = "/yonetim/hangfire";
    public const string KilitTemizlemeIsi = "durum-kilidi-temizle";

    public static bool HangfireEtkin(IConfiguration config) => config.GetValue("Hangfire:Etkin", false);

    public static IServiceCollection AddArkaPlan(this IServiceCollection services, IConfiguration config)
    {
        if (!HangfireEtkin(config))
        {
            services.AddSingleton<IBildirimKuyrugu, AnindaBildirimKuyrugu>();
            return services;
        }

        services.AddHangfire((sp, cfg) => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                sp.GetRequiredService<IConfiguration>().GetConnectionString("Veritabani"),
                new SqlServerStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));
        services.AddHangfireServer(o => o.WorkerCount = Math.Min(Environment.ProcessorCount, 4));
        services.AddSingleton<IBildirimKuyrugu, HangfireBildirimKuyrugu>();
        return services;
    }

    /// <summary>Veritabanı hazırlandıktan sonra çağrılır: ilk açılışta Hangfire şeması boş veritabanında kurulur.</summary>
    public static void UseArkaPlan(this WebApplication app)
    {
        if (!HangfireEtkin(app.Configuration))
        {
            return;
        }

        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<DurumKilidiTemizleyici>(
            KilitTemizlemeIsi, t => t.TemizleAsync(CancellationToken.None), Cron.Hourly(), new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        app.MapHangfireDashboard(PanoYolu, new DashboardOptions
        {
            DashboardTitle = "Arka plan işleri",
            AppPath = "/yonetim",
            DisplayStorageConnectionString = false,
            // Varsayılan filtre yalnız yerel isteğe izin verir, canlıda pano açılmaz. Yetkiyi uç politikası verir.
            Authorization = [new GirisliKullaniciFiltresi()],
        }).RequireAuthorization(Politikalar.Yonetici);
    }

    private sealed class GirisliKullaniciFiltresi : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => context.GetHttpContext().User.IsInRole("Yonetici");
    }
}
