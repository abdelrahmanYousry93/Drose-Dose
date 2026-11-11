using System.Net;
using Dose.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Dose.IntegrationTests;

[Collection(DbCollection.Name)]
public sealed class MarketingPublisherTests(DatabaseFixture fx)
{
    private static void RequireDb() =>
        Assert.SkipWhen(DatabaseFixture.ServerConnection is null, "Set DOSE_TEST_SQL to run SQL Server integration tests.");

    /// <summary>Records every call to the Graph API and answers like Meta does.</summary>
    private sealed class FakeGraph : HttpMessageHandler
    {
        public readonly List<(string Url, string Body)> Calls = [];
        public bool Fail;
        private int n;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            if (req.Method == HttpMethod.Get) return new(HttpStatusCode.OK) { Content = new StringContent("""{"name":"Dose"}""") };
            Calls.Add((req.RequestUri!.ToString(), await req.Content!.ReadAsStringAsync(ct)));
            if (Fail) return new(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"Invalid token"}}""") };
            n++;
            return new(HttpStatusCode.OK) { Content = new StringContent($$"""{"id":"m{{n}}","post_id":"page_p{{n}}"}""") };
        }
    }

    private WebApplicationFactory<Program> Api(string queuePath, FakeGraph graph) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("ConnectionStrings:Dose", fx.ConnectionString);
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Public:AppBaseUrl", "https://dose.test");
        b.UseSetting("Meta:PageId", "123");
        b.UseSetting("Meta:PageAccessToken", "test-token");
        b.UseSetting("Meta:QueuePath", queuePath);
        b.ConfigureServices(s => s.AddHttpClient("meta").ConfigurePrimaryHttpMessageHandler(() => graph));
    });

    [Fact]
    public async Task Due_posts_are_published_once_and_future_ones_wait()
    {
        RequireDb();
        var key = "t-" + Guid.NewGuid().ToString("N")[..8];
        var queue = Path.GetTempFileName();
        await File.WriteAllTextAsync(queue, $$"""
          [ { "key": "{{key}}-a", "kind": "album", "caption": "كابشن", "media": ["/m/w02/1.jpg", "/m/w02/2.jpg"], "publishAt": "{{DateTimeOffset.UtcNow.AddMinutes(-5):O}}" },
            { "key": "{{key}}-p", "kind": "photo", "caption": "صورة", "media": ["/m/w02/3.jpg"], "publishAt": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}" },
            { "key": "{{key}}-f", "kind": "video", "caption": "بعدين", "media": ["/m/w02/r.mp4"], "publishAt": "{{DateTimeOffset.UtcNow.AddDays(1):O}}" } ]
          """);
        var graph = new FakeGraph();
        await using var f = Api(queue, graph);

        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MarketingPublisher>().RunAsync(CancellationToken.None);

        // Album: two unpublished photos, then one feed post attaching both; then the single photo. Nothing for tomorrow.
        Assert.Equal(4, graph.Calls.Count);
        Assert.Contains("/v27.0/123/photos", graph.Calls[0].Url);
        Assert.Contains("published=false", graph.Calls[0].Body);
        Assert.Contains("url=https%3A%2F%2Fdose.test%2Fm%2Fw02%2F1.jpg", graph.Calls[0].Body);
        Assert.Contains("/123/feed", graph.Calls[2].Url);
        Assert.Contains("attached_media%5B1%5D", graph.Calls[2].Body);
        Assert.Contains("/123/photos", graph.Calls[3].Url);

        // A second run publishes nothing again.
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MarketingPublisher>().RunAsync(CancellationToken.None);
        Assert.Equal(4, graph.Calls.Count);

        var status = await f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://dose.test") })
            .GetStringAsync("/api/marketing/status");
        Assert.Contains($"{key}-a", status);
        Assert.Contains("page_p3", status);
        Assert.DoesNotContain("test-token", status);
    }

    [Fact]
    public async Task Failures_are_recorded_and_retried_at_most_three_times()
    {
        RequireDb();
        var key = "t-" + Guid.NewGuid().ToString("N")[..8];
        var queue = Path.GetTempFileName();
        await File.WriteAllTextAsync(queue, $$"""[ { "key": "{{key}}", "kind": "photo", "media": ["/m/x.jpg"], "publishAt": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}" } ]""");
        var graph = new FakeGraph { Fail = true };
        await using var f = Api(queue, graph);
        for (var i = 0; i < 5; i++)
        {
            using var scope = f.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MarketingPublisher>().RunAsync(CancellationToken.None);
        }
        Assert.Equal(3, graph.Calls.Count);
        var status = await f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://dose.test") })
            .GetStringAsync("/api/marketing/status");
        Assert.Contains("Invalid token", status);
    }
}
