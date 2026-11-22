using CihazServis.Application.Dogrulama;
using Microsoft.Extensions.DependencyInjection;

namespace CihazServis.Application;

public static class UygulamaKaydi
{
    public static IServiceCollection AddUygulama(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DogrulamaServisi>();
        services.AddScoped<Egitim.EgitimServisi>();
        services.AddScoped<Egitim.QuizServisi>();
        services.AddScoped<Egitim.SertifikaServisi>();
        services.AddScoped<Servis.ServisTalebiServisi>();
        services.AddScoped<Servis.MusteriCihazServisi>();
        services.AddScoped<Servis.DurumKilidiTemizleyici>();
        return services;
    }
}
