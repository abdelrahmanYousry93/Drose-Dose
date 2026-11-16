using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dose.Api;
using Dose.Domain.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Dose.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class StudentAccessTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    [Fact]
    public async Task Restore_link_brings_back_every_booking_of_that_number_and_nothing_else()
    {
        RequireDb();
        var session = await fx.CreateSessionAsync(SessionMode.Group, "جروب متوسط (5–9)");
        var clock = new FakeClock(fx.SeedTime);
        var phone = "0100" + Random.Shared.Next(1_000_000, 9_999_999);
        string mine, myRef, other;
        await using (var db = fx.NewDb())
        {
            var a = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
            await fx.Bookings(db, clock).SubmitDetailsAsync(a.AccessToken,
                new("طالب الرجوع", phone, null, null, AcceptTerms: true, AcceptTopUpTerms: true, ExpectedPrice: a.Price), default);
            (mine, myRef) = (a.AccessToken, a.Reference);
            var b = await fx.Bookings(db, clock).CreateHoldAsync(session.Id, default);
            await fx.Bookings(db, clock).SubmitDetailsAsync(b.AccessToken,
                new("طالب تاني", "0111" + Random.Shared.Next(1_000_000, 9_999_999), null, null, AcceptTerms: true, AcceptTopUpTerms: true, ExpectedPrice: b.Price), default);
            other = b.AccessToken;
        }

        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
            b.UseSetting("Jobs:Enabled", "false");
            b.UseSetting("Seed:LaunchCatalog", "false");
            // Bookings above were created with the fixture's token protector; the app must read them with the same one.
            b.ConfigureServices(s => s.AddSingleton(DatabaseFixture.Tokens));
        });
        var http = app.CreateClient();

        IssuedAccessLink issued;
        using (var scope = app.Services.CreateScope())
        {
            var s = scope.ServiceProvider.GetRequiredService<StudentAccessService>();
            var lookup = await s.LookupAsync(phone, default);
            Assert.Equal("طالب الرجوع", lookup.Name);
            Assert.Equal([myRef], lookup.Bookings.Select(x => x.Reference));
            issued = await s.IssueAsync(phone, "admin@test", default);
            await Assert.ThrowsAsync<Dose.Domain.Common.DomainException>(() => s.IssueAsync("01299999999", "admin@test", default));
        }
        Assert.Contains("/me/", issued.Link);
        Assert.Contains(issued.Link, issued.Message);
        Assert.DoesNotContain("?", issued.ChatUrl); // the private link never goes into a URL

        var token = issued.Link[(issued.Link.LastIndexOf('/') + 1)..];
        var restored = await http.GetFromJsonAsync<JsonElement>($"/api/me/{token}");
        var tokens = restored.GetProperty("bookings").EnumerateArray().Select(b => b.GetProperty("token").GetString()).ToList();
        Assert.Equal([mine], tokens);
        Assert.DoesNotContain(other, tokens);

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/me/" + new string('a', 43))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync($"/api/admin/students/lookup?phone={phone}")).StatusCode);
    }
}
