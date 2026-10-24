namespace Application.Abstractions;

/// <summary>Remembers which kinds of device each person has signed in from, so a new one can be told to them.</summary>
public interface IKnownDevices
{
    /// <summary>Records a sign-in from this device.</summary>
    /// <param name="userId">Who signed in.</param>
    /// <param name="device">The device's description, such as <c>Firefox · macOS</c>.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether it is new to an account that had signed in from something else before.</returns>
    Task<bool> RememberAsync(Guid userId, string device, DateTimeOffset now, CancellationToken cancellationToken);
}
