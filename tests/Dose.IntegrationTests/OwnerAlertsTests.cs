using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dose.Domain.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class OwnerAlertsTests(DatabaseFixture fx)
{
    private sealed class FakeTelegram : HttpMessageHandler
    {
        public readonly List<string> Sent = [];
        public readonly List<string> Urls = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Urls.Add(req.RequestUri!.AbsolutePath);
            if (req.RequestUri.AbsolutePath.EndsWith("/getUpdates"))
                return new(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true,"result":[{"update_id":1,"message":{"chat":{"id":777,"type":"private"},"text":"/start"}}]}""") };
            Sent.Add(await req.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"ok":true}""") };
        }
    }

    private WebApplicationFactory<Program> Api(FakeTelegram telegram, string? token) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Seed:LaunchCatalog", "false");
        b.UseSetting("Telegram:BotToken", token ?? "");
        b.UseSetting("Public:AppBaseUrl", "https://dose.test");
        b.ConfigureServices(s => s.AddHttpClient("telegram").ConfigurePrimaryHttpMessageHandler(() => telegram));
    });

    /// <summary>The API runs on the real clock, so the session must be in the real future.</summary>
    private async Task<Session> FutureSessionAsync()
    {
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        await using var db = fx.NewDb();
        var s = await db.Sessions.FindAsync(session.Id);
        var start = DateTime.UtcNow.AddDays(5);
        (s!.StartUtc, s.EndUtc, s.BookingDeadlineUtc, s.TierDecisionAtUtc) = (start, start.AddHours(1), start.AddHours(-24), start.AddHours(-24));
        await db.SaveChangesAsync();
        return s;
    }

    private static async Task<string> WaitForAsync(FakeTelegram t, int count)
    {
        for (var i = 0; i < 100 && t.Sent.Count < count; i++) await Task.Delay(50);
        Assert.True(t.Sent.Count >= count, $"expected {count} alerts, got {t.Sent.Count}");
        return JsonDocument.Parse(t.Sent[count - 1]).RootElement.GetProperty("text").GetString()!;
    }

    [Fact]
    public async Task Owner_is_alerted_when_a_booking_starts_and_when_details_are_entered()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        var session = await FutureSessionAsync();
        await using (var db = fx.NewDb()) // forget any chat linked by an earlier run
        {
            db.AuditLogs.RemoveRange(db.AuditLogs.Where(a => a.Entity == "TelegramChat"));
            await db.SaveChangesAsync();
        }
        var telegram = new FakeTelegram();
        using var app = Api(telegram, "test-token");
        var http = app.CreateClient();

        var hold = await (await http.PostAsJsonAsync("/api/bookings/holds", new { sessionId = session.Id })).Content.ReadFromJsonAsync<JsonElement>();
        var reference = hold.GetProperty("reference").GetString()!;
        var started = await WaitForAsync(telegram, 1);
        Assert.Contains("حد بدأ يحجز (لسه ماكتبش بياناته)", started);
        Assert.Contains(reference, started);
        Assert.Contains("https://dose.test/admin", started);
        // The chat is the person who sent /start to the bot.
        Assert.Equal("777", JsonDocument.Parse(telegram.Sent[0]).RootElement.GetProperty("chat_id").GetString());

        var res = await http.PostAsJsonAsync($"/api/b/{hold.GetProperty("accessToken").GetString()}/details", new
        {
            studentName = "طالب التنبيه", contactPhone = "01009990077", acceptTerms = true, acceptTopUpTerms = true,
            expectedPrice = hold.GetProperty("price").GetDecimal(),
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        var details = await WaitForAsync(telegram, 2);
        Assert.Contains("طالب كتب بياناته", details);
        Assert.Contains("طالب التنبيه", details);
        Assert.Contains("https://wa.me/201009990077", details);
        Assert.Single(telegram.Urls, u => u.EndsWith("/getUpdates")); // the chat is remembered
    }

    [Fact]
    public async Task Without_a_bot_token_nothing_is_sent_and_booking_still_works()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        var session = await FutureSessionAsync();
        var telegram = new FakeTelegram();
        using var app = Api(telegram, null);
        var res = await app.CreateClient().PostAsJsonAsync("/api/bookings/holds", new { sessionId = session.Id });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        await Task.Delay(300);
        Assert.Empty(telegram.Urls);
    }

    [Fact]
    public async Task Unpaid_bookings_get_one_follow_up_with_a_ready_whatsapp_message()
    {
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");
        var session = await FutureSessionAsync();
        var telegram = new FakeTelegram();
        using var app = Api(telegram, "test-token");
        var http = app.CreateClient();
        var hold = await (await http.PostAsJsonAsync("/api/bookings/holds", new { sessionId = session.Id })).Content.ReadFromJsonAsync<JsonElement>();
        await http.PostAsJsonAsync($"/api/b/{hold.GetProperty("accessToken").GetString()}/details", new
        {
            studentName = "سارة أحمد", contactPhone = "01009990088", acceptTerms = true, acceptTopUpTerms = true,
            expectedPrice = hold.GetProperty("price").GetDecimal(),
        });
        await WaitForAsync(telegram, 2);
        var reference = hold.GetProperty("reference").GetString()!;
        await using (var db = fx.NewDb())
        {
            var b = await db.Bookings.SingleAsync(x => x.Reference == reference);
            b.GetType().GetProperty("CreatedAtUtc")!.SetValue(b, DateTime.UtcNow.AddMinutes(-45));
            await db.SaveChangesAsync();
        }

        using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<Dose.Api.OwnerFollowUps>().RunAsync(default);
        using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<Dose.Api.OwnerFollowUps>().RunAsync(default);

        var followUp = await WaitForAsync(telegram, 3);
        Assert.Contains("متابعة: كتب بياناته ومدفعش لسه", followUp);
        Assert.Contains(reference, followUp);
        Assert.Contains("https://wa.me/201009990088?text=", followUp);
        await Task.Delay(300);
        Assert.Equal(3, telegram.Sent.Count(m => m.Contains(reference))); // started, details, one follow-up only
    }
}
