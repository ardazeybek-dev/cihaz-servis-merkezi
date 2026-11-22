using CihazServis.Application.Ortak;
using CihazServis.Application.Varliklar;
using CihazServis.Application.Egitim;
using CihazServis.Application.Servis;
using CihazServis.Infrastructure.Bildirim;
using CihazServis.Infrastructure.Depolama;
using CihazServis.Infrastructure.Belgeler;
using CihazServis.Infrastructure.Guvenlik;
using CihazServis.Infrastructure.Veri;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CihazServis.Infrastructure;

public static class AltyapiKaydi
{
    public static IServiceCollection AddAltyapi(this IServiceCollection services, IConfiguration config)
    {
        // Bağlantı dizesi çözümleme anında okunur: ortam değişkeni ve test ayarları da geçerli olur.
        services.AddDbContext<UygulamaDbContext>((sp, o) => o.UseSqlServer(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Veritabani") is { Length: > 0 } baglanti
                ? baglanti
                : throw new InvalidOperationException("ConnectionStrings__Veritabani tanımlı değil."),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", UygulamaDbContext.Sema)));
        services.AddScoped<IUygulamaDbContext>(sp => sp.GetRequiredService<UygulamaDbContext>());
        services.AddScoped<IPasswordHasher<Kullanici>, PasswordHasher<Kullanici>>();
        services.AddScoped<DemoVeriTohumlayici>();
        services.AddSingleton<IIpOzetleyici, HmacIpOzetleyici>();
        services.AddSingleton<IMedyaImzalayici, HmacMedyaImzalayici>();
        services.AddSingleton<ISertifikaPdfUretici, QuestPdfSertifikaUretici>();
        services.AddScoped<ITalepNoUretici, SqlTalepNoUretici>();
        services.AddSingleton<IDosyaDeposu, YerelDosyaDeposu>();
        services.AddSingleton<IEpostaGonderici, MailKitEpostaGonderici>();
        services.AddScoped<ServisBildirimIsleri>();
        return services;
    }
}
