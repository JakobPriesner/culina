using System.Reflection;

namespace ArchitectureTests;

/// <summary>
/// Conventions that are cheap to state and expensive to rediscover, enforced
/// as build failures rather than review comments.
/// </summary>
public class ForbiddenDependencyTests
{
    [Fact]
    public void NoAssembly_ShouldUseIOptions_BecauseSettingsAreInjectedDirectly()
    {
        // Arrange
        var assemblies = CulinaAssemblies.All;

        // Act
        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .Where(type => MembersOf(type).Any(MentionsOptions))
            .Select(type => type.FullName!);

        // Assert
        // A config value is a record, a record is a singleton, and a singleton
        // is injected directly. IOptions<T> would add a .Value to every
        // consumer and make the dependency read as "the options system".
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoAssembly_ShouldReferenceAScanningLibrary_BecauseRegistrationIsExplicit()
    {
        // Arrange
        var assemblies = CulinaAssemblies.All;

        // Act
        var offenders = assemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name!)
            .Where(name => name.Contains("Scrutor", StringComparison.OrdinalIgnoreCase));

        // Assert
        // Scanning turns a deleted or renamed service into a 500 at runtime
        // instead of a compile error, and defeats trimming.
        Assert.Empty(offenders);
    }

    [Fact]
    public void DomainAndContracts_ShouldNotUseDateTime_BecauseOffsetsAreExplicit()
    {
        // Arrange
        Assembly[] assemblies = [CulinaAssemblies.Domain, CulinaAssemblies.Contracts];

        // Act
        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .SelectMany(type => type.GetProperties(Everything).Select(property => (type, property)))
            .Where(pair => Unwrap(pair.property.PropertyType) == typeof(DateTime))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}");

        // Assert
        // A DateTime has no offset, so its meaning depends on where it was
        // created. Every timestamp in Culina is a DateTimeOffset in UTC.
        Assert.Empty(offenders);
    }

    [Fact]
    public void DomainAndContracts_ShouldNotUseBinaryFloatingPoint_BecauseQuantitiesMustBeExact()
    {
        // Arrange
        Assembly[] assemblies = [CulinaAssemblies.Domain, CulinaAssemblies.Contracts];

        // Act
        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .SelectMany(type => type.GetProperties(Everything).Select(property => (type, property)))
            .Where(pair => Unwrap(pair.property.PropertyType) is var kind
                && (kind == typeof(double) || kind == typeof(float)))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}");

        // Assert
        // A double cannot represent 0.1, and an ingredient amount of 0.1 litres
        // is ordinary. Quantities are decimal.
        Assert.Empty(offenders);
    }

    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static IEnumerable<Type> MembersOf(Type type) =>
        type.GetProperties(Everything).Select(property => property.PropertyType)
            .Concat(type.GetFields(Everything).Select(field => field.FieldType))
            .Concat(type.GetMethods(Everything).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)))
            .Concat(type.GetConstructors(Everything).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType)));

    private static bool MentionsOptions(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition().FullName is { } name
        && name.StartsWith("Microsoft.Extensions.Options.IOptions", StringComparison.Ordinal);
}
