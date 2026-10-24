using Application.Abstractions;
using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>The <see cref="IDeviceLanguage"/> port over the current request's <c>Accept-Language</c>.</summary>
internal sealed class HttpDeviceLanguage(IHttpContextAccessor accessor) : IDeviceLanguage
{
    public Language Current => accessor.HttpContext?.DeviceLanguage() ?? Language.En;
}
