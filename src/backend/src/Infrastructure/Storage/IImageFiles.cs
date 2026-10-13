namespace Infrastructure.Storage;

/// <summary>Where <see cref="ImageStore"/> keeps rendition bytes; it decides what they are and what they are called.</summary>
internal interface IImageFiles
{
    /// <summary>Stores a rendition whole or not at all; a key that already exists already holds these bytes.</summary>
    /// <remarks>Takes no token: a client hanging up is no reason to discard the renditions.</remarks>
    Task WriteAsync(string key, byte[] bytes);

    /// <summary>Writes one rendition to a destination; false when there is none under the key.</summary>
    Task<bool> CopyToAsync(string key, Stream destination, CancellationToken cancellationToken);

    /// <summary>Removes a rendition; one that is already gone is no failure.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
