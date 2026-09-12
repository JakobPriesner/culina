using Domain.Shared;
using Xunit.Sdk;

namespace TestSupport;

/// <summary>
/// Reads a <see cref="Result"/> in a test.
/// </summary>
/// <remarks>
/// <c>Match</c> is the only way to observe an outcome, which is exactly right
/// in production code and verbose in an assertion. These live in one place so
/// the pattern is not rewritten in every test, and they always compare the
/// error <b>code</b> — never the description, which is prose and will be
/// reworded.
/// </remarks>
public static class ResultAssertions
{
    /// <summary>Asserts success and returns the value.</summary>
    /// <typeparam name="TValue">What the operation produced.</typeparam>
    /// <param name="result">The outcome under test.</param>
    public static TValue ShouldBeSuccess<TValue>(this Result<TValue> result)
        where TValue : notnull =>
        result.Match(
            value => value,
            error => throw new XunitException(
                $"Expected success, but the operation failed with '{error.Code}': {error.Description}"));

    /// <summary>Asserts success.</summary>
    /// <param name="result">The outcome under test.</param>
    public static void ShouldBeSuccess(this Result result) =>
        result.Match(
            () => { },
            error => throw new XunitException(
                $"Expected success, but the operation failed with '{error.Code}': {error.Description}"));

    /// <summary>Asserts failure with the expected error code.</summary>
    /// <param name="result">The outcome under test.</param>
    /// <param name="expected">The error the operation should have returned.</param>
    public static void ShouldBeFailure(this Result result, Error expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        result.Match(
            () => throw new XunitException($"Expected failure '{expected.Code}', but it succeeded."),
            actual => AssertCode(expected, actual));
    }

    /// <summary>Asserts failure with the expected error code.</summary>
    /// <typeparam name="TValue">What a success would have produced.</typeparam>
    /// <param name="result">The outcome under test.</param>
    /// <param name="expected">The error the operation should have returned.</param>
    public static void ShouldBeFailure<TValue>(this Result<TValue> result, Error expected)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(expected);

        result.Match(
            _ => throw new XunitException($"Expected failure '{expected.Code}', but it succeeded."),
            actual => AssertCode(expected, actual));
    }

    /// <summary>Asserts failure and returns the error, for asserting on its causes.</summary>
    /// <typeparam name="TValue">What a success would have produced.</typeparam>
    /// <param name="result">The outcome under test.</param>
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
