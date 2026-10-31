using Dose.Application.Abstractions;
using Dose.Application.Administration;
using Dose.Application.Bookings;
using Dose.Application.Catalog;
using Dose.Application.Payments;
using Dose.Application.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace Dose.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDoseApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<SettingsService>();
        services.AddScoped<CatalogQueries>();
        services.AddScoped<SessionQueries>();
        services.AddScoped<BookingService>();
        services.AddScoped<PaymentReviewService>();
        services.AddScoped<AdminCatalogService>();
        services.AddScoped<AdminPricingService>();
        services.AddScoped<AdminSessionService>();
        return services;
    }
}
