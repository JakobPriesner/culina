---
name: dotnet-layer-mapping
description: How models cross layer boundaries in the culina-v2 backend — hand-written static extension-method mappers (ToGetResponse, ToDomain, ToRow) instead of AutoMapper or shared types. Use whenever a Domain entity must become a Contracts response, a request must become a command, or a database row must become a Domain model.
---

# Mapping between layers

A model belongs to exactly one layer. It crosses a boundary only by being
mapped, and mapping is always a **static class with static extension methods**,
written by hand.

No AutoMapper, no Mapster, no reflection-based mapper. A hand-written mapper is
greppable, appears in the call graph, is trim/AOT safe, fails at compile time
when a property is added, and costs one obvious method.

## Where a mapper lives

The mapper lives with the **target-side layer that owns the operation**, next
to the code that calls it:

| Direction                        | File                                                              |
| -------------------------------- | ----------------------------------------------------------------- |
| `Domain` → `Contracts` response  | `Application/<Domain>/<Operation>/<Entity>Mappings.cs`             |
| `Contracts` request → command    | `Api/Endpoints/<Domain>/<Operation>/V1/<Operation>RequestExtensions.cs` |
| database row → `Domain`          | `Infrastructure/Persistence/<Domain>/<Entity>RowMappings.cs`       |
| `Domain` → database parameters   | same file as the row mapper                                        |

Never a central `Mappings/` or `Mappers/` folder: a mapper is part of one
operation's slice, and an operation's shape changes with that operation.

## The shape

```csharp
namespace Application.Users.GetById;

/// <summary>Maps a user onto the shape this operation returns.</summary>
internal static class UserMappings
{
    internal static Response ToGetByIdResponse(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new Response
        {
            UserId = user.Id,
            DisplayName = user.DisplayName.Value,
            Email = user.Email.Value,
            JoinedAt = user.JoinedAt,
            Version = user.Version,
        };
    }
}
```

Rules:

- `internal static class`, `static` methods, `this` on the source parameter.
- Named for the destination, not the source: `ToGetByIdResponse`,
  `ToCreateCommand`, `ToDomain`, `ToRow`. A mapper per operation-shaped
  response, because each operation owns its own `Response`.
- **Pure.** No I/O, no `DateTimeOffset.UtcNow`, no service lookups, no
  `Result`. Anything a mapper cannot decide is passed in as a parameter:
  `ToGetByIdResponse(this User user, bool canEdit)`.
- Every property assigned explicitly. Object-initialiser syntax with `required`
  properties on the DTO means adding a field to the DTO breaks the build in
  the mapper — which is the point.
- Null handling at the boundary: `ArgumentNullException.ThrowIfNull` on the
  source (a null here is a defect, not an expected failure — see
  `dotnet-result-pattern`).
- Collections map with a `Select` over the single-item mapper, so there is one
  definition of how an item is shaped:
  `Items = [.. users.Select(user => user.ToSummary())]`.

## Request → Command

The endpoint does not construct commands field-by-field inline; it calls a
mapper in its own folder, so the endpoint body stays one handler call.

```csharp
namespace Api.Endpoints.Users.Create.V1;

internal static class CreateUserRequestExtensions
{
    internal static CreateUserCommand ToCommand(this Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateUserCommand(request.Email, request.DisplayName);
    }
}
```

This mapper is allowed to be trivial. It exists so that adding a field is one
edit in one predictable place, and so the endpoint never grows a body.

## Row → Domain

Infrastructure owns the persistence shape and never leaks it upward:

```csharp
namespace Infrastructure.Persistence.Users;

internal static class UserRowMappings
{
    internal static User ToDomain(this UserRow row) => ...;
}
```

An `Application` signature never mentions a row type, a `DbDataReader`, or an
`NpgsqlParameter`.

## What must never happen

- A `Domain` entity serialised directly to a response ("it has the same
  fields"). It will not have the same fields in three months, and the change
  will be a silent breaking change to every client.
- A `Contracts` type used as the input to a `Domain` constructor. Map to
  domain value objects, validating as you go (validation returns a `Result`;
  the mapper does not).
- A mapper that reaches for a service to fill a field. Compute the value in
  the handler and pass it in.
- A shared "universal" mapper used by several operations' responses.

## Checklist

- [ ] Static class, static extension method, in the operation's own folder.
- [ ] Named after the destination shape.
- [ ] Pure, total, every property explicit.
- [ ] No `Domain` type, row type, or Infrastructure type appears in a
      `Contracts` DTO or an `Application` public signature.
