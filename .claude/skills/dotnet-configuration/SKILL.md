---
name: dotnet-configuration
description: Configuration conventions for the culina-v2 backend — every config group is a record registered as a singleton and injected directly, never IOptions<T>/IOptionsSnapshot<T>/IOptionsMonitor<T>; bootstrap values are immutable and validated at startup, admin-editable instance settings are mutable singletons backed by the database. Use when adding, reading, exposing, or validating any setting.
---

# Configuration

**A config value is a record. A record is a singleton. A singleton is injected
directly.** There is no `IOptions<T>` anywhere in this codebase — not
`IOptions`, not `IOptionsSnapshot`, not `IOptionsMonitor`.

Why: `IOptions<T>` adds a wrapper with a `.Value` to every consumer, makes the
dependency read as "the options system" rather than "these settings", drags
reflection-based binding into the startup path, and offers a reload story this
app does not use. Consumers depend on the settings record the same way they
depend on a repository.

## Two kinds

| | Bootstrap settings | Instance settings |
| --- | --- | --- |
| Source | `appsettings.json`, environment variables | a row in the database |
| Changed by | an operator, with a restart | an admin, from the app's own UI |
| Shape | `sealed record`, `init`-only properties | `sealed record`, settable properties |
| Examples | connection parts, data directory, cookie name, trusted proxies, OTLP endpoint | feature flags, branding, thresholds, provider settings |

Decide with these questions, in order:

1. Is it needed *before* the database can be reached? → bootstrap.
2. Is it the key that protects a stored secret? → the key is bootstrap; the
   ciphertext it protects is an instance setting.
3. Is it host plumbing with no meaning to an admin (listen URLs, log levels)?
   → bootstrap.
4. Everything else → instance setting in the database.

Default to the database. An admin self-hosting this app may have no shell
access to edit a JSON file.

## Bootstrap settings

```csharp
// Application/Abstractions/Settings/DatabaseSettings.cs
public sealed record DatabaseSettings
{
    public const string SectionName = "Database";

    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Name { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool RequireSsl { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException($"{SectionName}:Host is required.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"{SectionName}:Port must be a valid port number.");
        }
    }
}
```

- Every bootstrap record has a `SectionName` constant and a `Validate()` that
  throws on anything unusable. An architecture test requires the `Validate()`;
  the composition root calls it at startup. **A misconfigured process must fail
  to start, loudly, not fail on the first request that needed the value.**
  This is the one place where throwing is correct — it is a defect in the
  deployment, not a request outcome.
- Read field by field from `IConfiguration`, not `GetSection(...).Get<T>()`:
  explicit reads need no reflection-based binder and give a better error.
- Connection details are configured as **parts**, never as one connection
  string, so the parts can be validated and a password can be sourced
  separately from the host.

```csharp
// Infrastructure/Settings/DatabaseSettingsExtensions.cs
public static IServiceCollection AddDatabaseSettings(this IServiceCollection services, IConfiguration configuration)
{
    var settings = new DatabaseSettings
    {
        Host = configuration[$"{DatabaseSettings.SectionName}:Host"] ?? throw Missing("Host"),
        Port = int.Parse(configuration[$"{DatabaseSettings.SectionName}:Port"] ?? "5432", CultureInfo.InvariantCulture),
        Name = configuration[$"{DatabaseSettings.SectionName}:Name"] ?? throw Missing("Name"),
        Username = configuration[$"{DatabaseSettings.SectionName}:Username"] ?? throw Missing("Username"),
        Password = configuration[$"{DatabaseSettings.SectionName}:Password"] ?? throw Missing("Password"),
        RequireSsl = bool.Parse(configuration[$"{DatabaseSettings.SectionName}:RequireSsl"] ?? "true"),
    };

    settings.Validate();

    return services.AddSingleton(settings);
}
```

## Instance settings

Same record idea, but the properties are settable, because live updates work by
mutating the one shared singleton every consumer already holds — no cache
invalidation, no re-resolution, no restart.

```csharp
public sealed record RegistrationSettings
{
    public const string GroupName = "registration";

    public bool OpenRegistration { get; set; }
    public bool RequireInvitation { get; set; } = true;
    public int MaxUsers { get; set; } = 100;
}
```

Read side — inject the record, use it:

```csharp
internal sealed class RegisterUserCommandHandler(RegistrationSettings registration, IUserRepository users)
    : ICommandHandler<RegisterUserCommand, Response>
{
    public async Task<Result<Response>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (!registration.OpenRegistration)
        {
            return UserErrors.RegistrationClosed;
        }
        // ...
    }
}
```

Write side — an ordinary command handler that injects the same singleton,
persists first, then mutates:

```csharp
internal sealed class UpdateRegistrationSettingsCommandHandler(
    RegistrationSettings settings,
    ISettingsStore<RegistrationSettings> store)
    : ICommandHandler<UpdateRegistrationSettingsCommand>
{
    public async Task<Result> Handle(UpdateRegistrationSettingsCommand command, CancellationToken cancellationToken)
    {
        var updated = settings with
        {
            OpenRegistration = command.OpenRegistration,
            RequireInvitation = command.RequireInvitation,
            MaxUsers = command.MaxUsers,
        };

        var saved = await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

        return saved.Tap(() =>
        {
            settings.OpenRegistration = updated.OpenRegistration;
            settings.RequireInvitation = updated.RequireInvitation;
            settings.MaxUsers = updated.MaxUsers;
        });
    }
}
```

Save before mutating: a failed save must never leave the process disagreeing
with the database.

**Accepted tradeoff:** properties are assigned one at a time, so a concurrent
reader could observe a group mid-update. For a single self-hosted instance with
admin-only, low-frequency writes this is fine — do not add locking. If a subset
genuinely cannot tolerate a torn read (a credential and the flag that enables
it), nest it in one immutable inner record and swap it with a single
assignment.

## Store and initial load

```csharp
public interface ISettingsStore<TSettings> where TSettings : class
{
    Task<Result<TSettings>> LoadAsync(CancellationToken cancellationToken);
    Task<Result> SaveAsync(TSettings settings, CancellationToken cancellationToken);
}
```

One JSONB row per group (`settings` table, keyed by `GroupName`), not a column
per value — a new checkbox must not be a schema migration. A hosted service
loads each group once, before the app serves traffic; if the row does not
exist, the record keeps its compiled-in defaults.

## Registration and exposure

One `Add<Group>Settings` extension per group, called explicitly from
`Infrastructure/DependencyInjection.cs`. One line per group, no scanning.

Instance settings are exposed to the admin UI as an ordinary resource pair —
`GET /api/v1/settings/registration`, `PUT /api/v1/settings/registration` —
behind an authorization policy, and **always through Contracts DTOs**, never by
serialising the settings record. That is what lets a secret be write-only
(`apiKey` in, `apiKeyConfigured: true` out — never the value or its
ciphertext back).

## Checklist

- [ ] Value is a `sealed record` in `Application/Abstractions/Settings/`,
      grouped as an admin would see it on one screen.
- [ ] Bootstrap → `init` properties, `SectionName`, `Validate()` called at
      startup. Instance → settable properties, `GroupName`, database-backed.
- [ ] Registered as a singleton by its own `Add<Group>Settings` extension.
- [ ] No `IOptions<T>` in any form; consumers inject the record.
- [ ] Secrets encrypted at rest and write-only in their Contracts shape.
