using CihazServis.Application;
using CihazServis.Infrastructure;
using CihazServis.Infrastructure.Veri;
using CihazServis.Web.Altyapi;
using CihazServis.Web.Api;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

builder.Services.AddUygulama();
builder.Services.AddAltyapi(builder.Configuration);
builder.Services.AddWebKatmani(builder.Configuration);

var app = builder.Build();

await VeritabaniHazirla(app);

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/durum/{0}");
app.UseGuvenlikBasliklari();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error : Serilog.Events.LogEventLevel.Information);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapOpenApi("/openapi/{documentName}.json");
app.MapApiUclari();
app.MapGet("/saglik", () => Results.Text("ok")).ExcludeFromDescription();

app.Run();

static async Task VeritabaniHazirla(WebApplication app)
{
    var ayar = app.Configuration.GetSection("Veritabani");
    await using var scope = app.Services.CreateAsyncScope();
    if (ayar.GetValue<bool>("OtomatikMigration"))
    {
        var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        await db.Database.MigrateAsync();
    }

    if (ayar.GetValue<bool>("DemoVerisi"))
    {
        await scope.ServiceProvider.GetRequiredService<DemoVeriTohumlayici>().TohumlaAsync();
    }
}

/// <summary>Entegrasyon testlerinde WebApplicationFactory için.</summary>
public partial class Program;
