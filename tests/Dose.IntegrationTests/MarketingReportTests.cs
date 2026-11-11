using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dose.Domain.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class MarketingReportTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private WebApplicationFactory<Program> Api() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Meta:PageId", "");
        b.UseSetting("Meta:PageAccessToken", "");
    });

    [Fact]
    public async Task Funnel_counts_steps_per_post_and_only_paid_holds_as_paid()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);

        // One booking that gets paid and approved, one that stays a hold.
        string paidRef, unpaidRef, token;
        await using (var db = fx.NewDb())
        {
            var paid = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
            (paidRef, token) = (paid.Reference, paid.AccessToken);
            await fx.Bookings(db, clock).SubmitDetailsAsync(token,
                new("طالب اختبار", "01009990001", null, null, AcceptTerms: true, AcceptTopUpTerms: true, ExpectedPrice: paid.Price), default);
            unpaidRef = (await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default)).Reference;
        }
        await using (var db = fx.NewDb())
        {
            using var png = DatabaseFixture.TinyPng();
            await fx.Bookings(db, clock).SubmitPaymentProofAsync(token, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, "TX-9", default);
            var hash = Dose.Application.Bookings.AccessTokens.Hash(token);
            var proofId = await db.PaymentProofs.Where(p => p.Booking!.AccessTokenHash == hash).Select(p => p.Id).SingleAsync();
            await fx.Payments(db, clock).ApproveAsync(proofId, "admin@test", default);
        }

        var src = "fb-test-" + Guid.NewGuid().ToString("N")[..8];
        using var app = Api();
        var http = app.CreateClient();
        async Task Track(string? source, string? step, string? reference = null) =>
            Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsJsonAsync("/api/track", new { source, step, reference })).StatusCode);

        await Track(src, "visit");
        await Track(src.ToUpperInvariant(), "visit");        // case-insensitive source
        await Track(src, "whatsapp");
        await Track(src, "hold", paidRef);
        await Track(src, "hold", unpaidRef.ToLowerInvariant());
        await Track(src, "request", "REQ-1234");
        // Malformed input is ignored: never stored, never an error.
        await Track("bad source!", "visit");
        await Track(src, "purchase");
        await Track(null, null);

        var json = await http.GetFromJsonAsync<JsonElement>("/api/marketing/report?days=7");
        var funnel = json.GetProperty("funnel").EnumerateArray().ToList();
        var row = funnel.Single(r => r.GetProperty("source").GetString() == src);
        Assert.Equal(2, row.GetProperty("visits").GetInt32());
        Assert.Equal(1, row.GetProperty("whatsApp").GetInt32());
        Assert.Equal(2, row.GetProperty("holds").GetInt32());
        Assert.Equal(1, row.GetProperty("requests").GetInt32());
        Assert.Equal(1, row.GetProperty("paid").GetInt32());
        Assert.DoesNotContain(funnel, r => r.GetProperty("source").GetString() == "bad source!");
        Assert.Empty(json.GetProperty("engagement").EnumerateObject());
    }
}
