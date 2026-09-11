---
name: dotnet-dependency-injection
description: Dependency-injection conventions for the culina-v2 backend — constructor (primary-constructor) injection everywhere, explicit per-layer registration extensions, no service locator or assembly scanning, and the lifetime rules for handlers, repositories, settings and endpoints. Use when registering a new service, choosing a lifetime, or introducing a new abstraction.
---

# Dependency injection

Every collaborator arrives through the constructor. Nothing calls
`IServiceProvider.GetService` in application code, nothing uses a static
singleton or an ambient context, and nothing is discovered by scanning.

## Primary constructors, interfaces owned by the consumer

```csharp
internal sealed class CreateUserCommandHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CreateUserCommandHandler> logger)
    : ICommandHandler<CreateUserCommand, Response>
```

- The **port** (`IUserRepository`, `IPasswordHasher`) is declared in
  `Application/Abstractions/`, because the consumer owns the interface it
  needs; the **adapter** lives in `Infrastructure/` and is the only thing that
  knows about Npgsql, HTTP, or the filesystem.
- Inject `TimeProvider` rather than calling `DateTimeOffset.UtcNow`, and
  inject an RNG abstraction rather than `Guid.NewGuid()` in code whose output
  a test needs to pin. Time and randomness are dependencies.
- Do not inject `IServiceProvider`, `IHttpContextAccessor` (wrap it: an
  `IUserContext` port in Application, implemented in Infrastructure), or a
  concrete class from another layer.
- More than about five constructor parameters is a signal the handler is doing
  two things — split the use case, do not add a facade to hide the count.

## Registration: explicit, one line each

One `DependencyInjection.cs` per layer, exposing one extension method,
composed in `Program.cs`:

```csharp
// Program.cs
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddPresentation()
    .AddEndpoints();
```

```csharp
// Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    return services
        .AddScoped<IQueryHandler<GetUserByIdQuery, GetUserByIdResponse>, GetUserByIdQueryHandler>()
        .AddScoped<ICommandHandler<CreateUserCommand, CreateUserResponse>, CreateUserCommandHandler>();
}
```

**No `Scrutor`, no `AddClassesAssignableTo`, no reflection.** Registration by
scanning means a handler can be deleted, renamed or forgotten and the failure
shows up as a 500 at runtime instead of a compile error; it also defeats
trimming and AOT. The cost is one line per handler, and an architecture test
asserts that every `ICommandHandler`/`IQueryHandler` implementation in the
assembly has a registration — so "I forgot the line" fails the build.

Group registrations by domain with a comment header, in the same order as the
folders. Nothing depends on the order itself.

## Lifetimes

| Lifetime    | Use for                                                                 |
| ----------- | ----------------------------------------------------------------------- |
| `Singleton` | Endpoints (`IEndpoint`), settings records, `NpgsqlDataSource`, `TimeProvider`, hashers, pure policy/strategy objects, caches. |
| `Scoped`    | Command/query handlers, repositories, unit of work, `IUserContext`, anything per-request. |
| `Transient` | Rarely. Only for cheap, stateless helpers a scoped consumer should not share. |

Hard rules:

- A singleton must never depend on a scoped service. If it seems to need one,
  it needs a factory (`IServiceScopeFactory`) at the composition root, or the
  dependency is misclassified. `ValidateScopes`/`ValidateOnBuild` are enabled
  in Development so this fails at startup, not in production.
- A singleton must be thread-safe. Mutable singletons are limited to the
  settings records described in `dotnet-configuration`.
- Endpoints inject their handler **as a delegate parameter**, not a
  constructor parameter, precisely because the endpoint is a singleton and the
  handler is scoped.
- `HttpClient` is never `new`ed: register a typed client through
  `IHttpClientFactory` with a resilience handler.

## Decorators

Cross-cutting behaviour (logging a use case, a validation pass, a transaction
boundary) is a hand-written decorator registered explicitly, not a pipeline
behaviour resolved by reflection:

```csharp
services.AddScoped<ICommandHandler<CreateUserCommand, Response>>(provider =>
    new LoggingCommandHandler<CreateUserCommand, Response>(
        new CreateUserCommandHandler(/* resolved dependencies */),
        provider.GetRequiredService<ILogger<CreateUserCommand>>()));
```

Prefer, though, putting the behaviour where it is visible: the observability
scope belongs in the handler (see `dotnet-observability`), and the transaction
belongs in the unit of work.

## Testing implication

Because everything is constructor-injected and every port is an interface owned
by Application, a unit test constructs the handler with fakes directly and
never builds a container. If a test needs `IServiceProvider`, the design is
wrong. See `dotnet-testing`.

## Checklist

- [ ] New service has an interface in `Application/Abstractions/` and an
      implementation in the layer that owns the technology.
- [ ] Registered explicitly, one line, in that layer's `DependencyInjection.cs`.
- [ ] Lifetime chosen from the table; no singleton captures a scoped service.
- [ ] No `GetRequiredService` outside the composition root.
- [ ] No assembly scanning was added.
