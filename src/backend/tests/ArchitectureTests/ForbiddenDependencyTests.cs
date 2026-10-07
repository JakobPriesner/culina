using System.Reflection;

namespace ArchitectureTests;

/// <summary>Conventions that are cheap to state and expensive to rediscover, enforced as build failures.</summary>
public class ForbiddenDependencyTests
{
    [Fact]
    public void NoCulinaSetting_ShouldBeWrappedInIOptions_BecauseSettingsAreInjectedDirectly()
    {
        var assemblies = CulinaAssemblies.All;

        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .Where(type => MembersOf(type).Any(WrapsACulinaTypeInOptions))
            .Select(type => type.FullName!);

        // Settings are injected directly, not via IOptions<T>. Only *our* settings: framework base classes
        // that demand IOptionsMonitor are not this problem.
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoAssembly_ShouldReferenceAScanningLibrary_BecauseRegistrationIsExplicit()
    {
        var assemblies = CulinaAssemblies.All;

        var offenders = assemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name!)
            .Where(name => name.Contains("Scrutor", StringComparison.OrdinalIgnoreCase));

        // Scanning turns a deleted service into a runtime 500 and defeats trimming.
        Assert.Empty(offenders);
    }

    [Fact]
    public void DomainAndContracts_ShouldNotUseDateTime_BecauseOffsetsAreExplicit()
    {
        Assembly[] assemblies = [CulinaAssemblies.Domain, CulinaAssemblies.Contracts];

        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .SelectMany(type => type.GetProperties(Everything).Select(property => (type, property)))
            .Where(pair => Unwrap(pair.property.PropertyType) == typeof(DateTime))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}");

        // A DateTime has no offset; every timestamp is a DateTimeOffset in UTC.
        Assert.Empty(offenders);
    }

    [Fact]
    public void DomainAndContracts_ShouldNotUseBinaryFloatingPoint_BecauseQuantitiesMustBeExact()
    {
        Assembly[] assemblies = [CulinaAssemblies.Domain, CulinaAssemblies.Contracts];

        var offenders = assemblies
            .SelectMany(CulinaAssemblies.TypesIn)
            .SelectMany(type => type.GetProperties(Everything).Select(property => (type, property)))
            .Where(pair => Unwrap(pair.property.PropertyType) is var kind
                && (kind == typeof(double) || kind == typeof(float)))
            .Select(pair => $"{pair.type.Name}.{pair.property.Name}");

        // A double cannot represent 0.1, and ingredient amounts are ordinary decimals.
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

    private static bool WrapsACulinaTypeInOptions(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition().FullName is { } name
        && name.StartsWith("Microsoft.Extensions.Options.IOptions", StringComparison.Ordinal)
        && type.GetGenericArguments()[0].Assembly.GetName().Name is { } wrapped
        && CulinaAssemblies.Contains(wrapped);
}
