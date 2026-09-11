---
name: dotnet-testing
description: xUnit testing conventions for the culina-v2 backend — the four test projects (Domain unit, Application unit, Architecture, Integration), naming, fakes over mocking frameworks, asserting on Result values, and what each layer is responsible for proving. Use when writing or reviewing any backend test, or deciding what level a behaviour should be tested at.
---

# Testing (xUnit)

Four projects, four jobs:

| Project | Proves | Touches |
| --- | --- | --- |
| `Domain.UnitTests` | Entities, value objects and policies enforce their invariants. | Nothing. No DI, no I/O. |
| `Application.UnitTests` | Each handler orchestrates correctly and returns the right `Result`. | In-memory fakes of the ports. |
| `ArchitectureTests` | The structural rules hold: layer references, endpoint layout, handler registration, error-code format, settings `Validate()`. | Assembly metadata only. |
| `IntegrationTests` | The wire behaves: routing, status codes, cookies, CSRF, headers, ETags, SQL. | Real API host + PostgreSQL via Testcontainers. |

Every behaviour is tested at the **lowest level that can prove it**. A rule
about who may edit a household is a `Domain` test, not an HTTP round trip.
Integration tests cover the things only the real pipeline can show.

## Naming and shape

```csharp
public class CreateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var users = new FakeUserRepository([UserFactory.WithEmail("taken@example.com")]);
        var handler = new CreateUserCommandHandler(users, new FakeUnitOfWork(), TimeProvider.System);

        // Act
        var result = await handler.Handle(new CreateUserCommand("taken@example.com", "Ada"), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeFailure(UserErrors.EmailAlreadyUsed);
    }
}
```

- One test class per production class, named `<Class>Tests`, in a folder
  mirroring the production folder.
- Method name: `<Method>_Should<Expected>_When<Condition>`.
- Arrange / Act / Assert, separated by blank lines and those three comments.
- `[Fact]` for one case, `[Theory]` + `[InlineData]`/`[MemberData]` for the
  same assertion over several inputs. A `[Theory]` with a branch inside it is
  two tests.
- Always pass the test's cancellation token to the code under test.
- No `Thread.Sleep`, no real clock (`TimeProvider` is injected — use
  `FakeTimeProvider`), no shared mutable static state, no ordering between
  tests. Tests run in parallel.

## Fakes, not a mocking framework

Ports are small interfaces this repo owns, so a hand-written fake in
`Application.UnitTests/Fakes/` is shorter and clearer than a mock setup, and it
fails to compile — rather than at runtime — when the interface changes.

```csharp
internal sealed class FakeUserRepository(IEnumerable<User>? seed = null) : IUserRepository
{
    private readonly List<User> users = [.. seed ?? []];

    public IReadOnlyList<User> Added { get; } = [];

    public Task<Result<User>> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(user => user.Id == id).ToResult(UserErrors.NotFound(id)));
}
```

Assert on the outcome (the returned `Result`, the fake's recorded state), not
on how many times a method was called. Interaction assertions ossify the
implementation; a test that breaks when a handler is refactored without a
behaviour change is a bad test.

Build entities through a small `*Factory`/builder in the test project with
sensible defaults and named overrides, so a test states only what it cares
about.

## Asserting on Result

`Result` exposes no `IsSuccess` for branching, so the test project defines
assertion helpers once:

```csharp
internal static class ResultAssertions
{
    internal static T ShouldBeSuccess<T>(this Result<T> result) =>
        result.Match(value => value, error => throw new XunitException($"Expected success, got {error.Code}."));

    internal static void ShouldBeFailure(this Result result, Error expected) =>
        result.Match(
            () => throw new XunitException($"Expected failure {expected.Code}, got success."),
            actual => Assert.Equal(expected.Code, actual.Code));
}
```

Assert on the **error code**, never the description — the description is prose
and will be reworded.

## Integration tests

- One `WebApplicationFactory` fixture plus a PostgreSQL `Testcontainers`
  container per collection; migrations run once, and each test gets a clean
  state (transaction rollback or a truncate helper).
- Drive the API through `HttpClient` exactly as a browser would, including the
  cookie and CSRF dance — that path is the thing being tested.
- Assert status code, problem `code`, and the headers that are part of the
  contract (`ETag`, `Location`, `X-Request-Id`, security headers).
- No mocking of internal services. Only genuinely external systems get a stub.

## What must have a test

- Every `Result` failure branch a handler can return.
- Every domain invariant, including the rejection case.
- Every security middleware rejection (missing CSRF, foreign origin, rate
  limit, unauthenticated, unauthorized).
- Every ETag path: 200 with an `ETag`, 304 on `If-None-Match`, 412 on a stale
  `If-Match`.
- Every bug fixed: the regression test comes first and fails before the fix.

Coverage percentage is not a target; the list above is.

## Checklist

- [ ] Test lives in the project that matches what it proves, mirroring the
      production folder.
- [ ] Named `<Method>_Should<Expected>_When<Condition>`, AAA-structured.
- [ ] Uses hand-written fakes and factories; asserts outcomes, not calls.
- [ ] Failure branches asserted by error code.
- [ ] Deterministic: injected time, no sleeps, no cross-test state.
