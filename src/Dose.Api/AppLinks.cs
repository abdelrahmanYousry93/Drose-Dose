using Dose.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dose.Api;

public sealed class AppLinks(IOptions<PublicSettings> settings) : IAppLinks
{
    public string BookingLink(string token) => $"{settings.Value.AppBaseUrl.TrimEnd('/')}/b/{token}";
}
