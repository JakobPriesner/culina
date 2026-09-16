namespace Api.Infrastructure;

/// <summary>
/// The HTTP methods the request guards let past without checking.
/// </summary>
/// <remarks>
/// Both the CSRF check and the same-origin check exempt these, and they are
/// only sound in doing so because no <c>GET</c> endpoint in Culina changes
/// state. One definition, so the two gates cannot disagree — a method added to
/// one copy and not the other would leave the weaker check as the only one a
/// request meets.
/// </remarks>
internal static class SafeMethods
{
    internal static bool Includes(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
}
