---
name: dotnet-result-pattern
description: The Result/Result<T>/Error failure model for the culina-v2 backend — expected failures are returned as values, exceptions are reserved for defects, and Match is the only way to unwrap. Use when writing or reviewing any handler, repository, domain method, or endpoint that can fail, or when defining a new <Domain>Errors entry.
---

# The result pattern

**Expected failure is a `Result`. A defect is an exception.** "The email is
already taken", "that recipe does not exist", "you may not edit this" are all
ordinary outcomes of a working system and are returned, never thrown. A null
reference, a broken invariant, a misconfigured service, a database that is
unreachable — those are defects and are allowed to throw, where the exception
handler turns them into a 500 and a logged error.

Adapted from `apps/backend/src/Domain/Shared/` in the culina repo; port those
files as-is rather than re-deriving them.

## The primitives (`Domain/Shared/`)

- `Result` — success or failure, no value.
- `Result<T>` — success with a `T`, or failure.
- `Error` — `record Error(string Code, string Description, ErrorType Type)`.
- `ErrorType` — the enum that transports switch on: `Failure`, `Validation`,
  `Problem`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`,
  `PreconditionFailed`, `RateLimited`, `Unavailable`.
- `ValidationError` — an `Error` carrying several contributing errors.
- `FieldError` — an `Error` that names the input field it is about.

Deliberate design points, do not "improve" them:

- **There is no `Error.None`.** A success has no error slot to fill, so no
  null-object error is needed and no code can ask a success for its error.
- **`Result` is a readonly struct** with a private constructor; build one with
  `Result.Success()`, `Result.Failure(error)`, or the implicit conversion from
  `Error` (`return UserErrors.NotFound;`).
- **`default(Result)` throws when observed**, because it is neither success nor
  failure — an uninitialised result is a bug, not a third state.
- **No `IsSuccess`/`Value` properties are exposed for branching.** `Match` is
  the only observation, which makes it impossible to read a value that is not
  there.

## Writing failures

Every module owns a static `<Domain>Errors` class in `Domain/<Domain>/`:

```csharp
public static class UserErrors
{
    public static readonly Error NotAuthenticated = new(
        "users.not_authenticated", "The request is not authenticated.", ErrorType.Unauthorized);

    public static Error NotFound(Guid userId) => new(
        "users.not_found", $"No user with id '{userId}' exists.", ErrorType.NotFound);

    public static readonly Error EmailAlreadyUsed = new(
        "users.email_already_used", "That email address is already registered.", ErrorType.Conflict);
}
```

Rules for error codes:

- **lowercase, `module.reason`, underscore-separated**: `users.invalid_email`,
  `households.member_limit_reached`. An architecture test enumerates every
  `<Domain>Errors` member and fails on anything else.
- The code is part of the API contract — clients branch on it. Renaming one is
  a breaking change; adding one is not.
- `Description` is a sentence for a human. It never contains a secret, a
  password, a token, or a raw SQL error.
- Errors are defined once, as statics or static factories, never constructed
  inline at a call site.

## Composing

```csharp
// Chain: each step runs only if the previous succeeded.
return Email.Create(command.Email)                       // Result<Email>
    .Bind(email => user.ChangeEmail(email))              // Result
    .Map(() => new Response { UserId = user.Id });        // Result<Response>

// Guard a value you already have.
return recipe.ToResult(RecipeErrors.NotFound(recipeId))
    .Ensure(r => r.OwnerId == userId, RecipeErrors.Forbidden);

// Report every validation failure at once instead of one per round trip.
return Result.Combine(
    ValidateTitle(command.Title),
    ValidateServings(command.Servings),
    ValidateIngredients(command.Ingredients));
```

`Result.Combine` produces a `ValidationError` holding each contributing
failure; those that are `FieldError`s become the `errors[]` array on the
problem document, so a form can mark every wrong box in one pass.

## Async

Handlers return `Task<Result<T>>`. Do not invent `Result<Task<T>>`. Where a
chain needs to continue over an await, either `await` first and then compose,
or use the `ResultAsyncExtensions` (`BindAsync`, `MapAsync`, `EnsureAsync`).
Always `.ConfigureAwait(false)` in Application/Infrastructure.

```csharp
public async Task<Result<Response>> Handle(GetUserQuery query, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(query);

    var found = await users.FindAsync(query.UserId, cancellationToken).ConfigureAwait(false);

    return found.Map(user => user.ToGetResponse());
}
```

## Unwrapping — exactly once, at the endpoint

```csharp
var result = await handler.Handle(new GetUserQuery(userId), cancellationToken)
    .ConfigureAwait(false);

return result.Match(Results.Ok, CustomResults.Problem);
```

`CustomResults.Problem(Error)` in `Api/Infrastructure/` is the single place
that maps an `ErrorType` to a status code and an RFC 9457 problem document:

| ErrorType           | Status |
| ------------------- | ------ |
| `Validation`        | 400    |
| `Unauthorized`      | 401    |
| `Forbidden`         | 403    |
| `NotFound`          | 404    |
| `Conflict`          | 409    |
| `PreconditionFailed`| 412    |
| `RateLimited`       | 429    |
| `Unavailable`       | 503    |
| `Failure`/`Problem` | 500    |

The document always carries `code` (the error code) and `requestId` (the
correlation id the request-context middleware assigned), plus `errors[]` for a
`ValidationError`. No endpoint builds a problem document by hand.

## Never

- `throw new NotFoundException(...)` for an expected miss, and never an
  exception filter that converts exceptions back into status codes for the
  normal path.
- `result.Value` / `result.Error` accessed behind an `if (result.IsSuccess)` —
  use `Match`.
- Swallowing an exception to return `Result.Failure` — a defect must stay a
  defect and be logged. The single exception: a well-understood, expected
  infrastructure failure (a unique-constraint violation racing a check) may be
  caught *in Infrastructure* and translated into the matching domain `Error`,
  with a comment naming the constraint.
- A `Result` returned up from a handler and then ignored. Every result is
  matched.

## Checklist

- [ ] Every failure a caller could reasonably handle is an `Error` in a
      `<Domain>Errors` class with a `module.reason` code.
- [ ] Handlers return `Task<Result<T>>`/`Task<Result>`; nothing throws for an
      expected outcome.
- [ ] `Match` appears exactly once per request, in the endpoint.
- [ ] Validation failures are combined so one round trip reports all of them.
- [ ] The error's `Description` leaks nothing a caller should not see.
