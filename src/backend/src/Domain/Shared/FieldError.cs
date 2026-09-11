namespace Domain.Shared;

/// <summary>
/// A validation error that names the input field it is about, so a form can
/// mark the offending control rather than showing a general message.
/// </summary>
/// <param name="Field">
/// The field name as it appears in the request body, in camelCase.
/// </param>
/// <param name="Code">The machine-readable code, formatted <c>module.reason</c>.</param>
/// <param name="Description">A sentence for a human.</param>
public sealed record FieldError(string Field, string Code, string Description)
    : Error(Code, Description, ErrorType.Validation);
