using System.Reflection;
using System.Text.RegularExpressions;
using Domain.Shared;

namespace ArchitectureTests;

/// <summary>
/// Error codes are part of the API contract: clients branch on them, so
/// renaming one is a breaking change and the format must not drift.
/// </summary>
public partial class ErrorCodeTests
{
    [Fact]
    public void EveryErrorCode_ShouldBeModuleDotReason_WhereverItIsDeclared()
    {
        // Arrange
        var declared = DeclaredErrors().ToList();

        // Act
        var offenders = declared
            .Where(entry => !CodeFormat().IsMatch(entry.Error.Code))
            .Select(entry => $"{entry.Owner}: '{entry.Error.Code}'");

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryErrorDescription_ShouldReadAsASentence_BecauseItIsShownToPeople()
    {
        // Arrange
        var declared = DeclaredErrors().ToList();

        // Act
        var offenders = declared
            .Where(entry => entry.Error.Description.Length < 8
                || !StartsCapitalised(entry.Error.Description)
                || !entry.Error.Description.EndsWith('.'))
            .Select(entry => $"{entry.Owner}: '{entry.Error.Description}'");

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void ErrorsClasses_ShouldExist_SoTheRuleIsActuallyBeingChecked()
    {
        // Arrange & Act
        var declared = DeclaredErrors().ToList();

        // Assert
        // Guards against the rules above passing because they found nothing.
        Assert.NotEmpty(declared);
    }

    /// <summary>
    /// The first letter, ignoring a leading quote: several descriptions open by
    /// quoting the offending value.
    /// </summary>
    private static bool StartsCapitalised(string description) =>
        description.FirstOrDefault(char.IsLetter) is var first && char.IsUpper(first);

    [GeneratedRegex("^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$")]
    private static partial Regex CodeFormat();

    private static IEnumerable<(string Owner, Error Error)> DeclaredErrors()
    {
        const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var errorClasses = CulinaAssemblies.All
            .SelectMany(CulinaAssemblies.TypesIn)
            .Where(type => type.IsAbstract && type.IsSealed)
            .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal));

        foreach (var type in errorClasses)
        {
            foreach (var field in type.GetFields(Statics).Where(field => field.FieldType == typeof(Error)))
            {
                if (field.GetValue(null) is Error error)
                {
                    yield return ($"{type.Name}.{field.Name}", error);
                }
            }

            foreach (var method in type.GetMethods(Statics).Where(method => method.ReturnType == typeof(Error)))
            {
                if (Invoke(method) is Error error)
                {
                    yield return ($"{type.Name}.{method.Name}", error);
                }
            }
        }
    }

    /// <summary>
    /// Error factories take the ids they describe, so they are called with
    /// harmless defaults purely to read the code they produce.
    /// </summary>
    private static Error? Invoke(MethodInfo method)
    {
        var arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType switch
            {
                var type when type == typeof(Guid) => (object?)Guid.Empty,
                var type when type == typeof(string) => "x",
                var type when type == typeof(int) => 0,
                var type when type == typeof(long) => 0L,
                var type when type.IsValueType => Activator.CreateInstance(type),
                _ => null
            })
            .ToArray();

        return method.Invoke(null, arguments) as Error;
    }
}
