namespace Domain.Shared;

/// <summary>
/// An expected failure, returned as a value rather than thrown.
/// </summary>
/// <param name="Code">
/// The machine-readable code, formatted <c>module.reason</c>. Clients branch on
/// it, so it is part of the API contract: adding one is safe, renaming one is a
/// breaking change.
/// </param>
/// <param name="Description">
/// A sentence for a human. It never contains a secret, a token, a password or a
/// raw database message, because it is returned to the caller verbatim. It is
/// English whatever the request asked for: the web client says the failure in
/// the reader's language by its code, so a new code needs a
/// <c>problem.&lt;code&gt;</c> message in the frontend's catalogues, and the
/// frontend's <c>explain.spec.ts</c> fails until it has one.
/// </param>
/// <param name="Type">The kind of failure, which decides the status code.</param>
/// <remarks>
/// There is deliberately no <c>Error.None</c>: a success has no error slot to
/// fill, so no null-object error is needed and no code can ask a success for
/// its error.
/// </remarks>
public record Error(string Code, string Description, ErrorType Type);
