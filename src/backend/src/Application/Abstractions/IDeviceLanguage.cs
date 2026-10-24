using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Which language the device making the current request reads, for words sent to somebody with no preference.</summary>
public interface IDeviceLanguage
{
    /// <summary>The device's language, or English outside a request.</summary>
    Language Current { get; }
}
