---
name: http-caching-etags
description: How culina-v2 uses ETags end to end — version-derived ETags on reads, If-None-Match for 304 responses, If-Match for optimistic concurrency on writes, and the SvelteKit client that stores and replays them. Use when adding a read endpoint, implementing a concurrent update, or making the frontend avoid refetching unchanged data.
---

# ETags and conditional requests

Two problems, one mechanism:

1. **Don't resend unchanged data.** The client replays the ETag as
   `If-None-Match`; the server answers `304 Not Modified` with no body.
2. **Don't let two writers clobber each other.** The client sends the ETag as
   `If-Match`; the server rejects a stale write with `412`.

## The ETag is a version, not a hash

Every entity that is read or updated individually carries a monotonic `version`
(`bigint`, incremented on each write). The ETag is derived from it:

```csharp
internal static class ETag
{
    internal static string Of(long version) => $"\"v{version}\"";

    internal static long? Read(string? headerValue) => ...;  // "v42" -> 42, anything else -> null
}
```

Hashing the serialised body is rejected on purpose: it costs a full render on
every request, changes when an unrelated field's formatting changes, and gives
no concurrency token. A version is cheap, stable, and already the thing
optimistic concurrency needs.

- Strong ETags (no `W/` prefix) — the comparison is exact.
- `version` is exposed in the response body as well, so a client that already
  holds the object knows its version without parsing a header.
- A list endpoint's ETag is derived from the collection's own version
  (`max(version)` plus the row count, or a per-collection counter), or it
  simply has no ETag. Never fabricate one from the query string.

## Read side

```csharp
app.MapGet("api/v1/recipes/{recipeId:guid}", async (
        Guid recipeId,
        HttpContext context,
        IQueryHandler<GetRecipeQuery, Response> handler,
        CancellationToken cancellationToken) =>
    {
        var result = await handler.Handle(new GetRecipeQuery(recipeId), cancellationToken).ConfigureAwait(false);

        return result.Match(recipe => ETag.Ok(context, recipe, recipe.Version), CustomResults.Problem);
    })
```

`ETag.Ok` sets the header and returns `200`, or returns `304` when the
request's `If-None-Match` already matches the current version. A `304` carries
the `ETag` and `Cache-Control` headers and **no body**.

Authenticated responses stay private: `Cache-Control: private, no-cache`
(revalidate every time) plus the ETag — never `public`, and never `max-age`
on user data. `no-cache` means "ask first", which is exactly the conditional
request this is about; `no-store` is for responses that must not be kept at
all (login, tokens).

Cost note: a `304` still runs the query. That is accepted — the win is
bandwidth and client rendering, not database load. If a specific read becomes
hot enough to matter, store the version in a cheap lookup and check it before
loading the full aggregate.

## Write side

```csharp
app.MapPut("api/v1/recipes/{recipeId:guid}", async (
        Guid recipeId,
        Request request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        IQueryHandler<...> handler,
        CancellationToken cancellationToken) =>
    {
        var command = request.ToCommand(recipeId, ETag.Read(ifMatch));
        ...
    })
```

- **`If-Match` is required on every `PUT`/`PATCH`/`DELETE` of a versioned
  resource.** Missing header → `428 Precondition Required` with code
  `<module>.precondition_required`. Unparsable or stale → `412` with
  `<module>.version_conflict`.
- The expected version travels into the command as data; the handler passes it
  to the repository, which enforces it **in the `UPDATE`'s `WHERE` clause**
  (`WHERE id = @id AND version = @expected`) and returns a conflict error when
  no row was affected. Never a read-then-compare-then-write: that is the race
  it is supposed to close.
- The write's response carries the **new** ETag, so the client can continue
  without a refetch.

## The client side

One place handles all of this — the API client wrapper
(`frontend-api-client`), never individual components:

- A small in-memory `Map<url, { etag, data }>`. On a `GET`, send
  `If-None-Match` when an entry exists; on `304`, return the stored data; on
  `200`, replace the entry.
- The cache is memory-only and cleared on logout. Never `localStorage`: private
  data must not outlive the session on disk.
- Every entity kept in a store retains its `version`. A mutation sends
  `If-Match: "v<version>"` from the version the user actually saw.
- On `412` the optimistic update rolls back and the UI says the item changed
  elsewhere, offering to reload — it never retries blindly, which would
  overwrite the other writer.
- Static assets are handled by the build's content hashes and immutable
  caching, not by this mechanism.

## Checklist

- [ ] Entity has a monotonic `version`, incremented by every write, exposed in
      the response body.
- [ ] Read endpoint sets a strong `ETag` and returns `304` on a matching
      `If-None-Match`, with `Cache-Control: private, no-cache`.
- [ ] Unsafe endpoint requires `If-Match`; missing → `428`, stale → `412`.
- [ ] The version check lives in the SQL `WHERE`, not in application code.
- [ ] The write response returns the new `ETag`.
- [ ] Client stores etag+data in memory only, clears on logout, rolls back on
      `412` instead of retrying.
- [ ] Integration tests cover `200`, `304`, `412`, and `428`.
