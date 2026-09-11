using System.Reflection;
using Application;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ArchitectureTests;

/// <summary>
/// The endpoint and handler conventions from <c>dotnet-endpoints</c>. They pass
/// vacuously until the first endpoint exists, which is why each rule that could
/// be satisfied by an empty set is paired with a guard elsewhere.
/// </summary>
public class EndpointConventionTests
{
    [Fact]
    public void EveryEndpoint_ShouldLiveInAnOperationVersionFolder_MatchingItsNamespace()
    {
        // Arrange
        var endpoints = Endpoints().ToList();

        // Act
        var offenders = endpoints
            .Where(type => !VersionedNamespace(type.Namespace))
            .Select(type => type.FullName!);

        // Assert
        // Api.Endpoints.<Domain>.<Operation>.V{n} — the folder carries the
        // version, so a v2 lands beside v1 with the same class name.
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryEndpoint_ShouldBeInternalAndSealed_BecauseNothingResolvesItByType()
    {
        // Arrange
        var endpoints = Endpoints().ToList();

        // Act
        var offenders = endpoints
            .Where(type => type.IsPublic || !type.IsSealed)
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryEndpoint_ShouldBeNamedVerbEntityEndpoint_SoARouteIsFindableByName()
    {
        // Arrange
        var endpoints = Endpoints().ToList();

        // Act
        var offenders = endpoints
            .Where(type => !type.Name.EndsWith("Endpoint", StringComparison.Ordinal))
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryHandler_ShouldBeInternalAndSealed_BecauseOnlyItsInterfaceIsResolved()
    {
        // Arrange
        var handlers = Handlers().ToList();

        // Act
        var offenders = handlers
            .Where(type => type.IsPublic || !type.IsSealed)
            .Select(type => type.FullName!);

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryHandler_ShouldBeRegistered_SoAForgottenLineFailsTheBuild()
    {
        // Arrange
        var handlers = Handlers().ToList();
        var services = new ServiceCollection().AddApplication();
        var registered = services
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is not null)
            .ToHashSet();

        // Act
        var offenders = handlers
            .Where(handler => !registered.Contains(handler))
            .Select(handler => handler.FullName!);

        // Assert
        // Registration is explicit, so this is the check that makes "I forgot
        // the line" a compile-time problem rather than a 500 in production.
        Assert.Empty(offenders);
    }

    private static bool VersionedNamespace(string? ns) =>
        ns is not null
        && ns.StartsWith("Api.Endpoints.", StringComparison.Ordinal)
        && ns.Split('.') is { Length: >= 5 } parts
        && parts[^1].Length > 1
        && parts[^1][0] == 'V'
        && parts[^1][1..].All(char.IsDigit);

    private static IEnumerable<Type> Endpoints() =>
        CulinaAssemblies.TypesIn(CulinaAssemblies.Api)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces().Any(i => i.Name == "IEndpoint"));

    private static IEnumerable<Type> Handlers() =>
        CulinaAssemblies.TypesIn(CulinaAssemblies.Application)
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces().Any(IsHandlerInterface));

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(ICommandHandler<>)
            || type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
            || type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));
}
