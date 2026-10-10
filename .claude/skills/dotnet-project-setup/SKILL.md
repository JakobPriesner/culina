---
name: dotnet-project-setup
description: How the culina-v2 backend solution is laid out — the Api/Application/Domain/Contracts/Infrastructure projects, who may reference whom, and the repo-wide build configuration in Directory.Build.props, Directory.Packages.props and .editorconfig. Use when creating a project, adding a NuGet package, changing a csproj, or deciding which layer a type belongs in.
---

# Backend solution layout and build configuration

## The five projects

```
src/backend/
  Directory.Build.props        repo-wide MSBuild properties — every csproj inherits these
  Directory.Packages.props     every package version, pinned once, centrally
  backend.slnx
  src/
    Domain/                    leaf. Entities, value objects, policies, Shared/ result primitives, <Domain>Errors
    Contracts/                 leaf. Request/Response DTOs, one folder per operation
    Application/               use cases (Command/Query + handler), ports in Abstractions/
    Infrastructure/            adapters: persistence, identity, external clients, settings stores
    Api/                       endpoints, middleware, composition root
  tests/
    Domain.UnitTests/
    Application.UnitTests/
    ArchitectureTests/
    IntegrationTests/
    TestSupport/               fakes and Result assertions shared by the test projects
```

| Project          | May reference             |
| ---------------- | ------------------------- |
| `Domain`         | nothing                   |
| `Contracts`      | nothing                   |
| `Application`    | `Domain`, `Contracts`     |
| `Infrastructure` | `Application` (and so `Domain`, `Contracts`) |
| `Api`            | `Infrastructure`, `Application`, `Contracts` |

The direction never reverses. `Domain` does not know that HTTP, JSON or a
database exist; `Contracts` is a bag of DTOs with no behaviour and no
dependency on `Domain`. An architecture test asserts the table above — a
`ProjectReference` that breaks it fails the build, not review.

Why `Contracts` is a leaf and not part of `Api`: an `Application` handler
returns the API-shaped response directly, so `Api` never re-shapes data. That
only works if both can see the DTOs without either depending on the other.

## What goes where — the one-line test

- A rule that would still be true if the app had no API and no database →
  `Domain`.
- A rule about *this use case* — orchestration, authorization, transaction
  boundary → `Application`.
- A fact about a *technology* — SQL text, HTTP client, cookie format,
  hashing algorithm → `Infrastructure`.
- A fact about *the wire* — route, status code, header, JSON shape →
  `Api` / `Contracts`.

## Directory.Build.props

One file at `src/backend/` (plus `tests/Directory.Build.props` for what the
test projects share), no per-project copies of these properties. Every property
carries a comment saying why it is set; a property without a reason is a
property nobody dares remove later. Read the file itself rather than a copy:
it targets `net10.0` with `LangVersion` and `AnalysisLevel` at `latest`, treats
warnings as errors with `AnalysisMode` `All` and `EnforceCodeStyleInBuild` (a
clean compile is also a clean lint), manages package versions centrally, and
restores from `packages.lock.json` in locked mode in CI and the image build.

`tests/Directory.Build.props` sets `<IsTestProject>true</IsTestProject>`
(`TestSupport` sets it back to `false`). If a
property is needed by exactly one project (`PublishAot`, `UserSecretsId`,
`OutputType`), it belongs in that csproj, not here.

## Directory.Packages.props

Central package management is on, so **no `Version=` attribute ever appears in
a csproj**. A csproj says `<PackageReference Include="Npgsql" />` and nothing
more; the version lives once in `Directory.Packages.props`, grouped by the
layer that consumes it, with a comment for any package whose presence is not
self-evident (a pin above a transitive version, a security advisory, a
licence-driven choice).

```xml
<Project>
  <ItemGroup>
    <!-- Infrastructure -->
    <PackageVersion Include="Npgsql" Version="…" />

    <!-- Api -->
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="…" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="…" />

    <!-- Tests -->
    <PackageVersion Include="xunit.v3" Version="…" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="…" />
  </ItemGroup>
</Project>
```

Adding a package is therefore two edits: the version here, the reference in
the one project that uses it. Adding it to `Directory.Build.props` as a global
`PackageReference` is not done — it makes every project depend on everything
and silently defeats the layering rules above.

## .editorconfig

One `.editorconfig` at the repository root, covering the whole repo (C#, TS,
Svelte, JSON, YAML, Markdown), because formatting arguments should be settled
by a file and not in review. It is the source of truth for style; `dotnet
format` and Prettier both read from it (Prettier via `editorconfig: true`).

Non-negotiable entries:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{ts,js,svelte,json,jsonc,yml,yaml,css,html}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.cs]
# Style rules are errors, matching TreatWarningsAsErrors — a build that
# compiles is a build that is formatted.
dotnet_diagnostic.IDE0055.severity = error
csharp_style_namespace_declarations = file_scoped:error
csharp_prefer_braces = true:error
dotnet_style_require_accessibility_modifiers = always:error
csharp_style_var_for_built_in_types = false:suggestion
dotnet_style_prefer_collection_expression = true:suggestion
```

Never disable a rule inline with `#pragma warning disable` to get a build
green. Either the rule is wrong for this repo — change `.editorconfig`, with a
comment — or the code is wrong.

## Checklist for a new project

- [ ] Lives under `src/backend/src/` (or `tests/`) and is added to `backend.slnx`.
- [ ] Its `ProjectReference`s obey the layering table; no `Version=` on any
      `PackageReference`.
- [ ] No properties duplicated from `Directory.Build.props`.
- [ ] An architecture test covers whatever new structural rule it introduces.
