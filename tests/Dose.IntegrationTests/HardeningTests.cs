using System.Net;
using System.Net.Http.Json;
using Dose.Application.Administration;
using Dose.Application.Payments;
using Dose.Domain.Common;
using Dose.Domain.Sessions;
using Dose.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class HardeningTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    /// <summary>The real API in Production mode against the test database (jobs off, HTTPS client).</summary>
    private WebApplicationFactory<Program> Api() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Public:AppBaseUrl", "https://dose.test");
    });

    private static HttpClient Client(WebApplicationFactory<Program> f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://dose.test"), AllowAutoRedirect = false });

    [Fact]
    public async Task Api_responses_carry_security_headers_and_are_not_cached()
    {
        RequireDb();
        await using var f = Api();
        var res = await Client(f).GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", res.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(res.Headers.CacheControl?.NoStore);
        Assert.True(res.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Plain_http_is_redirected_to_https_in_production()
    {
        RequireDb();
        await using var f = Api();
        var http = f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://dose.test"), AllowAutoRedirect = false });
        var res = await http.GetAsync("/api/health");
        Assert.True(res.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect or HttpStatusCode.Redirect);
        Assert.Equal("https", res.Headers.Location?.Scheme);
    }

    [Fact]
    public async Task State_changing_calls_from_another_site_are_blocked()
    {
        RequireDb();
        await using var f = Api();
        var client = Client(f);

        var evil = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email = "a@b.c", password = "x" }) };
        evil.Headers.Add("Origin", "https://evil.example");
        var blocked = await client.SendAsync(evil);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("http.cross_site", await blocked.Content.ReadAsStringAsync());

        var ours = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email = "a@b.c", password = "x" }) };
        ours.Headers.Add("Origin", "https://dose.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(ours)).StatusCode);
    }

    [Fact]
    public async Task Admin_api_requires_login_and_unknown_api_paths_are_404()
    {
        RequireDb();
        await using var f = Api();
        var client = Client(f);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/payment-proofs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/does-not-exist")).StatusCode);
    }

    [Fact]
    public async Task Encryption_keys_are_stored_in_the_database_so_links_survive_restarts()
    {
        RequireDb();
        string protectedText;
        await using (var f = Api())
            protectedText = f.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("hello");

        await using var db = fx.NewDb();
        Assert.True(await db.DataProtectionKeys.AnyAsync());

        await using var restarted = Api(); // a fresh app instance (new process after a redeploy) can still read it
        Assert.Equal("hello", restarted.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Unprotect(protectedText));
    }

    [Fact]
    public async Task Payment_screenshots_are_deleted_after_the_retention_period_but_the_record_stays()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group);
        var clock = new FakeClock(fx.SeedTime);
        Guid proofId;
        string fileKey;
        await using (var db = fx.NewDb())
        {
            var svc = fx.Bookings(db, clock);
            var hold = await svc.CreateHoldAsync(session.Id, default);
            await svc.SubmitDetailsAsync(hold.AccessToken, new Dose.Contracts.SubmitDetailsRequest("سارة", "01114445556", null, null, true, true, hold.Price), default);
            using var png = DatabaseFixture.TinyPng();
            await svc.SubmitPaymentProofAsync(hold.AccessToken, await DatabaseFixture.PaymentMethodIdAsync(db), png, png.Length, null, default);
            var proof = await db.PaymentProofs.SingleAsync(p => p.BookingId == hold.BookingId);
            (proofId, fileKey) = (proof.Id, proof.FileKey);
        }

        ProofRetention Retention(DoseDbContext db) => new(db, fx.Storage, new SettingsService(db, clock), clock);

        // Session ended 100 days ago → still inside the 180-day window.
        await using (var db = fx.NewDb())
        {
            await db.Sessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(u => u
                .SetProperty(s => s.StartUtc, clock.UtcNow.AddDays(-100).AddHours(-1)).SetProperty(s => s.EndUtc, clock.UtcNow.AddDays(-100)));
            await Retention(db).PurgeAsync(default);
            Assert.Null((await db.PaymentProofs.AsNoTracking().SingleAsync(p => p.Id == proofId)).FileDeletedAtUtc);
        }

        // 200 days ago → screenshot removed from storage, payment record kept.
        await using (var db = fx.NewDb())
        {
            await db.Sessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(u => u
                .SetProperty(s => s.StartUtc, clock.UtcNow.AddDays(-200).AddHours(-1)).SetProperty(s => s.EndUtc, clock.UtcNow.AddDays(-200)));
            Assert.True(await Retention(db).PurgeAsync(default) >= 1);
        }
        await using (var db = fx.NewDb())
        {
            var proof = await db.PaymentProofs.AsNoTracking().SingleAsync(p => p.Id == proofId);
            Assert.NotNull(proof.FileDeletedAtUtc);
            Assert.True(proof.Amount > 0);
            Assert.Null(await fx.Storage.OpenAsync(fileKey, default));
            var ex = await Assert.ThrowsAsync<DomainException>(() => fx.Payments(db, clock).OpenFileAsync(proofId, default));
            Assert.Equal("payment.file_deleted", ex.Code);
            Assert.Equal(0, await Retention(db).PurgeAsync(default)); // idempotent
        }
    }
}
