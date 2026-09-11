---
name: dotnet-endpoints
description: How an HTTP endpoint is built in the culina-v2 backend — the IEndpoint interface and explicit registration, the Domain/Operation/V1 folder slice, the Command or Query file, and the Request/Response contract types that are always mapped from internal models. Use whenever adding, versioning, or reviewing an endpoint or its contracts.
---

# Endpoints

One property must stay true: **the endpoint class contains no logic and no
validation.** It binds a request, calls exactly one handler, and maps the
result. Everything else lives elsewhere.

## The folder slice

The unit of organisation is the **operation**, not the type kind. Top level is
the domain (the API resource); inside it, one folder per operation; inside
that, one folder per version.

```
Api/Endpoints/Users/GetById/V1/GetUserByIdEndpoint.cs
Api/Endpoints/Users/GetById/V1/GetUserByIdRequestExtensions.cs   (only if it binds a body)
Api/Endpoints/Users/Create/V1/CreateUserEndpoint.cs

Application/Users/GetById/GetUserByIdQuery.cs                     query + handler, one file
Application/Users/Create/CreateUserCommand.cs

Contracts/Users/GetById/Response.cs
Contracts/Users/Create/Request.cs
Contracts/Users/Create/Response.cs
```

- `<Domain>` is **one** API resource — the first path segment after
  `api/v{n}/`, PascalCased. Never an umbrella folder covering several
  resources.
- `<Operation>` is a verb phrase naming what it does: `GetById`, `GetAll`,
  `Create`, `Update`, `Delete`, `TransferOwnership`. The same name is used in
  all three layers.
- `V{n}` holds everything that is specific to that version. When an operation
  gains a v2, the v1 folder is left untouched and `V2/` lands beside it with
  the same class names — the folder carries the version, not the class name.
- Never group a version folder's files by type kind (`V1/Models/`,
  `V1/Extensions/`). The folder is the whole slice.
- One `.cs` file per type, except that a command/query record and its handler
  share one file, since neither is useful without the other.

`ArchitectureTests` fails the build on an endpoint outside an operation/version
folder, on a class not named `<Verb><Entity>Endpoint`, and on a file in an
endpoint folder that is neither the endpoint nor its request extensions.

## IEndpoint

```csharp
// Api/Endpoints/IEndpoint.cs
namespace Api.Endpoints;

internal interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
```

Every endpoint is an `internal sealed class` implementing it, registered and
mapped **explicitly, by name** — never by assembly scanning. Scanning is a
reflection dependency: it breaks trimming/AOT, hides the route list from
`grep`, and makes a forgotten endpoint a runtime surprise rather than a
compile-time one.

```csharp
// Api/Extensions/EndpointExtensions.cs
internal static class EndpointExtensions
{
    internal static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        return services
            .AddUsersEndpoints()
            .AddHouseholdsEndpoints();
    }

    internal static WebApplication MapEndpoints(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }
}

// Api/Endpoints/Users/UsersEndpoints.cs — one file per domain, one line per endpoint
internal static class UsersEndpoints
{
    internal static IServiceCollection AddUsersEndpoints(this IServiceCollection services)
    {
        return services
            .AddSingleton<IEndpoint, GetUserByIdEndpoint>()
            .AddSingleton<IEndpoint, CreateUserEndpoint>();
    }
}
```

Endpoints are stateless, so they register as singletons. Grouping is for
reading only — routing matches on specificity and nothing depends on order.

## The endpoint itself

```csharp
using Application.Abstractions.Messaging;
using Application.Users.GetById;
using Api.Infrastructure;
using Response = Contracts.Users.GetById.Response;

namespace Api.Endpoints.Users.GetById.V1;

/// <summary>Reads one user.</summary>
internal sealed class GetUserByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("api/v1/users/{userId:guid}", async (
                Guid userId,
                HttpContext context,
                IQueryHandler<GetUserByIdQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetUserByIdQuery(userId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(user => ETag.Ok(context, user, user.Version), CustomResults.Problem);
            })
            .WithName("getUserByIdV1")
            .WithTags(Tags.Users)
            .WithSummary("Get a user")
            .WithDescription("Returns one user by id, with an ETag for a later conditional request.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
```

- Handlers are injected **per delegate parameter**, not into the class
  constructor — the endpoint is a singleton and the handler is scoped.
- `.WithName` is camelCase `<operation><Entity>V{n}` and is globally unique;
  it becomes the client's operation id.
- Declare every status code the endpoint can produce with `Produces` /
  `ProducesProblem`. The OpenAPI document is the frontend's generated client,
  so an undeclared response is a response the frontend cannot see.
- Route constraints (`{userId:guid}`) are binding, not validation. Real
  validation lives in the handler and returns a `Validation` error.

## Command and Query

One file per operation in `Application/<Domain>/<Operation>/`, holding the
record and its handler:

```csharp
namespace Application.Users.GetById;

public sealed record GetUserByIdQuery(Guid UserId);

internal sealed class GetUserByIdQueryHandler(IUserRepository users)
    : IQueryHandler<GetUserByIdQuery, Response>
{
    public async Task<Result<Response>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await users.FindAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return found.Map(user => user.ToGetByIdResponse());
    }
}
```

- **Query** = reads, no observable state change. **Command** = writes.
  A `GET` endpoint never dispatches a command.
- The record is `public` (the endpoint constructs it); the handler is
  `internal sealed` (nothing outside Application resolves it by type).
- Interfaces live in `Application/Abstractions/Messaging/`:
  `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`,
  `IQueryHandler<TQuery, TResponse>`. No mediator library — the endpoint
  injects the handler interface directly. That keeps the call graph
  navigable and costs one DI registration per handler.

## Request and Response — always mapped, never shared

Internal models (`Domain` entities, database rows) never appear on the wire.
Every operation owns its own DTOs in `Contracts/<Domain>/<Operation>/`, and
`Application` maps to them via the extension-method mappers described in
`dotnet-layer-mapping`.

```csharp
namespace Contracts.Users.Create;

public sealed record Request
{
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}

public sealed record Response
{
    public required Guid UserId { get; init; }
    public required long Version { get; init; }
}
```

Rules that exist to keep operations independently evolvable:

- **Each operation has its own `Response`.** Never return a shared `UserDto`
  from both `GetById` and `GetAll` — adding a field for one then changes the
  other. Share by inheritance if useful (`public sealed record Response :
  UserSummary;`), never by reuse of the same type.
- **A collection is always wrapped**: `Response(IReadOnlyList<UserSummary>
  Items, string? NextCursor, int Total)`, never a bare array at the top level.
  A bare array has nowhere to grow paging metadata.
- DTOs are plain data: no behaviour, no validation attributes, no reference to
  `Domain`, no `DateTime` (use `DateTimeOffset`), no `decimal`-as-`double`.
- A field is added freely; a field is renamed or removed only with a new
  version folder.

## Checklist

- [ ] Folder is `Api/Endpoints/<Domain>/<Operation>/V{n}/` and the class is
      `<Verb><Entity>Endpoint`, `internal sealed`, implementing `IEndpoint`.
- [ ] Registered by name in `<Domain>Endpoints.cs` and reachable from
      `AddEndpoints()`.
- [ ] Route follows `rest-api-design`; `.WithName` is unique and ends `V{n}`.
- [ ] Exactly one handler call; no `if` on domain state in the endpoint.
- [ ] Own `Request`/`Response` in `Contracts/<Domain>/<Operation>/`, mapped
      from internal models — no `Domain` type on the wire.
- [ ] Every producible status code declared; auth requirement explicit.
- [ ] Result unwrapped once via `result.Match(..., CustomResults.Problem)`.
