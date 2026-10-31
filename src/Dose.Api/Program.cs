using Dose.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddOptions<PublicSettings>()
    .Bind(builder.Configuration.GetSection(PublicSettings.SectionName))
    .ValidateOnStart();

const string WebClientCors = "web-client";
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy(WebClientCors, policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(WebClientCors);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Non-sensitive settings the student UI needs (e.g. the WhatsApp click-to-chat number).
app.MapGet("/api/public-settings", (Microsoft.Extensions.Options.IOptions<PublicSettings> settings) =>
    Results.Ok(settings.Value));

app.Run();
