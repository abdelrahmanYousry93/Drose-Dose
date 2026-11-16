using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class PartnerTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    private WebApplicationFactory<Program> Api() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Seed:LaunchCatalog", "false");
    });

    [Fact]
    public async Task Pledges_add_up_on_the_wall_and_only_named_ones_show()
    {
        RequireDb();
        await using var app = Api();
        var http = app.CreateClient();
        var tag = "t" + Guid.NewGuid().ToString("N")[..8];

        var named = await http.PostAsJsonAsync("/api/partners/pledges", new
        {
            organization = "مؤسسة اختبار " + tag, contactName = "أحمد", contactPhone = "01001234567", role = "CSR",
            interest = "fund", doses = 80, showOnWall = true, note = (string?)null, source = $"{tag}-density",
        });
        Assert.Equal(HttpStatusCode.OK, named.StatusCode);
        var reference = (await named.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetString();
        Assert.StartsWith("P-", reference);

        var anonymous = await http.PostAsJsonAsync("/api/partners/pledges", new
        {
            organization = "شركة سرية", contactName = "منى", contactPhone = "01111234567", role = (string?)null,
            interest = "fund", doses = 20, showOnWall = false, note = (string?)null, source = $"{tag}-ohm",
        });
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);

        // Funding without a number of doses, and a bad phone, are rejected.
        var noDoses = await http.PostAsJsonAsync("/api/partners/pledges", new
        {
            organization = "x", contactName = "y", contactPhone = "01001234567", interest = "fund", doses = 0, showOnWall = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, noDoses.StatusCode);
        var badPhone = await http.PostAsJsonAsync("/api/partners/pledges", new
        {
            organization = "x", contactName = "y", contactPhone = "12", interest = "students", showOnWall = false,
        });
        Assert.Equal(HttpStatusCode.BadRequest, badPhone.StatusCode);

        var wall = await http.GetFromJsonAsync<JsonElement>($"/api/partners/wall?src={tag}");
        Assert.Equal(100, wall.GetProperty("totalDoses").GetInt32());
        Assert.Equal(2, wall.GetProperty("organizations").GetInt32());
        var names = wall.GetProperty("names").EnumerateArray().Select(n => n.GetProperty("organization").GetString()).ToList();
        Assert.Equal(["مؤسسة اختبار " + tag], names);

        // The admin list is not public.
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/admin/partners")).StatusCode);
    }
}
