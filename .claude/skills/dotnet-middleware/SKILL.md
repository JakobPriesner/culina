---
name: dotnet-middleware
description: The ASP.NET Core middleware pipeline for the culina-v2 backend — what each middleware does, the order it must run in, and the rule that cross-cutting concerns (correlation, security headers, cookies/session, CSRF, origin checks, rate limiting) live in middleware rather than in endpoints. Use when adding a middleware, changing pipeline order, or deciding where a cross-cutting concern belongs.
---

# Middleware

Anything that must happen for **every** request belongs in middleware, not
repeated in endpoints. An endpoint that starts with a security check is a
missing middleware; the next endpoint will forget the check.

What belongs here: correlation, security headers, cookie/session resolution,
CSRF, origin checks, rate limiting, exception handling, request logging,
forwarded headers, static/SPA fallback. What does not: business authorization
decisions that depend on the resource being acted on (those are
`Result.Failure(ErrorType.Forbidden)` from the handler), and anything one
endpoint needs.

## Order (`Program.cs`)

Order is the contract. Each entry says why it is where it is.

```csharp
app.UseForwardedHeaders();      // 1. Real client IP/scheme, before anything that reads them.
app.UseRequestContext();        // 2. Request id + logging scope, so every later line is correlated.
app.UseSecurityHeaders();       // 3. Set before any handler can start writing a body.
app.UseHttpLogging();           // 4. One summary line per request.
app.UseExceptionHandler();      // 5. Outside everything below, so any defect becomes a problem document.
app.UseStatusCodePages();       // 6. Framework-generated statuses get a problem body too.
app.UseStaticFiles();           // 7. Cheap; never reaches auth.
app.UseQueryParameterGuard();   // 8. Reject malformed/duplicated query input before binding.
app.UseSameOriginGuard();       // 9. Unsafe cookie-authenticated requests must come from our origin.
app.UseRateLimiter();           // 10. Before authentication, so brute force costs nothing to reject.
app.UseAuthentication();        // 11. Cookie -> principal.
app.UseSessionContext();        // 12. Principal -> session record, user id on the logging scope + span.
app.UseCsrfGuard();             // 13. Needs the session to compare the token against.
app.UseAuthorization();         // 14. Policies, after identity is fully established.
app.MapHealthEndpoints();
app.MapEndpoints();
```

Moving one of these is a security change, not a refactor. The pipeline order is
covered by integration tests that assert, for example, that an unauthenticated
unsafe cross-origin request is rejected before it reaches a handler.

## Writing one

```csharp
namespace Api.Middleware;

internal sealed class RequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<RequestContextMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n");

        context.Items[RequestContext.RequestIdKey] = requestId;
        context.Response.Headers[CulinaHeaders.RequestId] = requestId;

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId });

        await next(context).ConfigureAwait(false);
    }
}
```

- `internal sealed class` in `Api/Middleware/`, one class per concern,
  convention-based `InvokeAsync`.
- Constructor injection is for singletons only (the middleware is a singleton);
  scoped services arrive as `InvokeAsync` parameters.
- Register through a named extension in `Api/Extensions/MiddlewareExtensions.cs`
  (`UseRequestContext()`), never `app.UseMiddleware<T>()` inline in
  `Program.cs` — the extension name is what makes the pipeline readable.
- A middleware that rejects a request writes a **problem document**, using the
  same `CustomResults.Problem` mapping endpoints use, with the same `code`
  vocabulary. A raw `context.Response.StatusCode = 403; return;` is not
  acceptable — the frontend parses problem documents.
- A rejection is logged at `Warning` with the reason and the request id, and
  increments its counter (see `dotnet-observability`).
- Never buffer the request or response body to inspect it. If a check needs the
  body, it is not a middleware.

## The security middlewares

Their behaviour is specified in `cookie-auth-and-security`; this skill only
fixes where they sit. In short:

- **SecurityHeadersMiddleware** — the one place any security header is set.
- **SameOriginMiddleware** — unsafe methods with a cookie must carry an
  `Origin`/`Referer` matching the app's own origin.
- **CsrfMiddleware** — unsafe cookie-authenticated requests must also carry the
  per-session CSRF token header.
- **SessionContextMiddleware** — turns the authenticated principal into the
  session record, and puts the user id on the logging scope and the current
  span.

## Checklist

- [ ] The concern really is per-request and cross-cutting; otherwise it belongs
      in a handler or an endpoint filter.
- [ ] Placed deliberately in the ordered list above, with a comment saying why
      that position.
- [ ] Registered via a named `Use*` extension.
- [ ] Rejections produce a problem document, a `Warning` log, and a counter.
- [ ] Covered by an integration test that asserts the rejection, not just the
      happy path.
