namespace Domain.Shared;

/// <summary>An expected failure, returned as a value rather than thrown. There is deliberately no <c>Error.None</c>.</summary>
/// <param name="Code">The machine-readable <c>module.reason</c> code; clients branch on it, so renaming one is a breaking change.</param>
/// <param name="Description">
/// An English sentence returned verbatim, so it never contains a secret or raw database message. The web client
/// translates by code: a new code needs a <c>problem.&lt;code&gt;</c> frontend message (<c>explain.spec.ts</c> enforces it).
/// </param>
/// <param name="Type">The kind of failure, which decides the status code.</param>
public record Error(string Code, string Description, ErrorType Type);
