namespace Api.Infrastructure;

/// <summary>The HTTP methods the request guards let past without checking.</summary>
/// <remarks>
/// Both the CSRF and same-origin checks exempt these, sound only because no <c>GET</c> changes
/// state. One definition, so the gates cannot disagree.
/// </remarks>
internal static class SafeMethods
{
    internal static bool Includes(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
}
