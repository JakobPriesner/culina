using Domain.Shared;
using Xunit.Sdk;

namespace TestSupport;

/// <summary>Reads a <see cref="Result"/> in a test, always comparing the error <b>code</b>, never the reworded description.</summary>
public static class ResultAssertions
{
    /// <summary>Asserts success and returns the value.</summary>
    public static TValue ShouldBeSuccess<TValue>(this Result<TValue> result)
        where TValue : notnull =>
        result.Match(
            value => value,
            error => throw new XunitException(
                $"Expected success, but the operation failed with '{error.Code}': {error.Description}"));

    public static void ShouldBeSuccess(this Result result) =>
        result.Match(
            () => { },
            error => throw new XunitException(
                $"Expected success, but the operation failed with '{error.Code}': {error.Description}"));

    /// <summary>Asserts failure with the expected error code.</summary>
    public static void ShouldBeFailure(this Result result, Error expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        result.Match(
            () => throw new XunitException($"Expected failure '{expected.Code}', but it succeeded."),
            actual => AssertCode(expected, actual));
    }

    /// <summary>Asserts failure with the expected error code.</summary>
    public static void ShouldBeFailure<TValue>(this Result<TValue> result, Error expected)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(expected);

        result.Match(
            _ => throw new XunitException($"Expected failure '{expected.Code}', but it succeeded."),
            actual => AssertCode(expected, actual));
    }

    /// <summary>Asserts failure and returns the error, for asserting on its causes.</summary>
    public static Error ShouldBeFailure<TValue>(this Result<TValue> result)
        where TValue : notnull =>
        result.Match<Error>(
            _ => throw new XunitException("Expected failure, but the operation succeeded."),
            error => error);

    private static void AssertCode(Error expected, Error actual)
    {
        if (!string.Equals(expected.Code, actual.Code, StringComparison.Ordinal))
        {
            throw new XunitException($"Expected failure '{expected.Code}', but got '{actual.Code}'.");
        }
    }
}
