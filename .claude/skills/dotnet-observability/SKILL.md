---
name: dotnet-observability
description: The logging, tracing and metrics strategy for the culina-v2 backend — what is logged at which level, structured logging with source-generated LoggerMessage, the correlation id and scope, OpenTelemetry traces/metrics/logs wiring, and the redaction rules. Use whenever adding a log statement, a span, a metric, or diagnosing why something is not visible in production.
---

# Observability: logging, tracing, metrics

One rule underneath everything: **a production incident must be answerable from
the telemetry alone.** Nobody can attach a debugger to a self-hosted instance,
and nobody may ask the operator to reproduce with more logging enabled.

OpenTelemetry carries all three signals. Traces and metrics come from the OTel
SDK; logs come from `ILogger` and are exported through the OTel logging
provider when a collector is configured. Culina invents no configuration names
of its own: the standard `OTEL_*` environment variables control export, and
when `OTEL_EXPORTER_OTLP_ENDPOINT` is unset the app still logs to the console
and simply exports nothing.

## What is logged, at which level

| Level | Meaning | Examples |
| --- | --- | --- |
| `Critical` | The process cannot serve. Emitted once, at startup or on a fatal health transition. | Configuration invalid, migration failed, database unreachable at boot. |
| `Error` | A **defect**: an unhandled exception, a failed background job, a broken invariant. Always has an exception attached. Never an expected failure. | Unhandled exception in a request, settings save threw, outbound call failed after retries. |
| `Warning` | A **security-relevant or degraded** outcome that the system handled. | Failed login, CSRF rejection, rate limit hit, authorization denied, deprecated route used, retry succeeded after failure. |
| `Information` | A **state change worth auditing**: something was created, updated, deleted, or a session began/ended. One line per business event, not per step. | User registered, household created, session issued, settings updated, migration applied. |
| `Debug` | The shape of an operation for a developer reproducing behaviour: chosen branch, query parameters, cache hit/miss. Off in production. | "Resolved 3 candidate recipes for query", SQL statement name and duration. |
| `Trace` | Firehose. Never enabled outside local development, never contains request bodies. | |

Consequences worth stating explicitly:

- **An expected failure (`Result.Failure`) is not an `Error`.** A 404 is not a
  problem with the server. Log a validation/not-found outcome at `Debug`, or at
  `Warning` only if it is security-relevant (auth, authz, rate limit, CSRF).
- **Every unhandled exception is logged exactly once**, by the exception
  handler middleware. Do not `catch`, log, and rethrow — that produces the same
  stack twice and hides the original site.
- **No log line inside a tight loop.** Aggregate and log a summary with counts.
- **Success is logged once, by the writer.** Read paths log nothing beyond the
  automatic HTTP request summary — otherwise the log is unreadable and the
  disk fills for free.

## How a log line is written

Source-generated `LoggerMessage` partial methods only. They are allocation-free
on the disabled path, force a compile-time-checked template, and cannot be
accidentally interpolated.

```csharp
internal static partial class UserLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "User {UserId} registered with role {Role}")]
    internal static partial void UserRegistered(this ILogger logger, Guid userId, string role);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Login failed for {EmailHash}: {Reason}")]
    internal static partial void LoginFailed(this ILogger logger, string emailHash, string reason);
}
```

- **Never** `logger.LogInformation($"User {userId} registered")`. String
  interpolation destroys the structure that makes a log queryable, and
  evaluates even when the level is disabled.
- Event ids are unique per domain, allocated in blocks (`1000-1099` users,
  `1100-1199` households, …) so an alert can key on an id that survives a
  message rewording.
- Property names are PascalCase and stable; they are the field names an
  operator queries. Renaming one breaks dashboards, so treat it like renaming
  an API field.
- Log classes live beside the code they serve: `Application/Users/UserLogs.cs`.

## Correlation

`RequestContextMiddleware` runs first in the pipeline and, for every request:

1. Reads the incoming `traceparent` (OTel does the real work) and takes the
   current `Activity.TraceId` as the **request id**, or generates one when
   there is no activity.
2. Pushes a logging scope `{ RequestId }`, so every line emitted while handling
   that request carries it.
3. Sets the `X-Request-Id` response header.
4. Puts the same value on the problem document as `requestId`.

So a user can paste the id from an error toast and an operator can find every
line and the whole trace. Do not add `traceId`/`spanId` to scopes by hand —
`ActivityTrackingOptions` is set to `None` and the collector reads those from
the exported span.

## Tracing

```csharp
public static class CulinaTelemetry
{
    public const string Name = "Culina";

    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
}
```

Automatic instrumentation covers ASP.NET Core (incoming), HttpClient
(outgoing), Npgsql (database) and the runtime. What it cannot see is the use
case, so **each command/query handler opens one span**:

```csharp
using var activity = CulinaTelemetry.ActivitySource.StartActivity("Users.Create");
activity?.SetTag("culina.user_id", userId);
```

- Span name is `<Domain>.<Operation>` — the folder path, so a span points at
  the code.
- Tags are low-cardinality by default. An id is acceptable as a tag; free text,
  an email, or a search query is not — high-cardinality tags are what make a
  tracing backend fall over.
- On a failed `Result`, set `activity?.SetStatus(ActivityStatusCode.Error,
  error.Code)` — the error code, never the description.
- Health-check requests are filtered out of tracing; they are 90% of the
  traffic and 0% of the information.

## Metrics

Three kinds, defined once on the shared `Meter`:

- A **histogram per use case**: `culina.usecase.duration` with tags
  `usecase` and `outcome` (`success` / the error code's module). Answers "what
  got slow" without a trace search.
- **Counters for things worth alerting on**: `culina.auth.login_failures`,
  `culina.csrf.rejections`, `culina.ratelimit.rejections`,
  `culina.settings.updates`.
- Everything else comes free from the ASP.NET Core, HttpClient, Npgsql and
  runtime instrumentation — request rate, status distribution, GC, thread pool,
  connection pool. Do not re-implement any of these by hand.

Never put a user id, path parameter, or free-text value in a metric tag.

## HTTP request summary

`UseHttpLogging` emits **one combined line per request** (method, path, status,
duration), enriched by an interceptor with the authenticated user id and the
request id. One line per request, not one on arrival and one on completion.
Request and response **bodies are never logged**, in any environment.

## Redaction — what must never reach a log or a span

Passwords and password hashes, session cookies and tokens, API keys and their
ciphertext, `Authorization` and `Cookie` header values, CSRF tokens, full email
addresses (log a stable hash or the domain part), any personal free text
(recipe notes, messages), and raw request/response bodies.

If a value is needed for support, log its shape instead: a hash, a length, a
last-4, or a boolean.

## Wiring

`Api/Extensions/ObservabilityExtensions.cs` owns the whole setup:

- Resource attributes: `service.name = culina-api`, `service.version` from the
  assembly, `deployment.environment.name` from the hosting environment.
- Tracing: `AddSource(CulinaTelemetry.Name)`, ASP.NET Core (health filtered),
  HttpClient, Npgsql.
- Metrics: `AddMeter(CulinaTelemetry.Name)`, ASP.NET Core, HttpClient, runtime,
  Npgsql.
- Logging: scopes on; simple single-line console in Development, JSON console
  otherwise; OTel logging provider with `IncludeFormattedMessage` when a
  collector is configured.
- `UseOtlpExporter()` once, covering all three signals, only when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

## Checklist for a new log or span

- [ ] Level matches the table — an expected `Result` failure is not `Error`.
- [ ] Written as a source-generated `LoggerMessage` with a unique event id.
- [ ] No interpolation, no secrets, no bodies, no PII.
- [ ] Structural fields named for querying, stable over time.
- [ ] A write path logs one `Information` line; a read path logs nothing extra.
- [ ] A new use case opens one `<Domain>.<Operation>` span and records its
      duration histogram with an `outcome` tag.
- [ ] Anything an operator should alert on has a counter, not just a log line.
