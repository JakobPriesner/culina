# Contributing

## Getting it running

You need Docker, the .NET 10 SDK, Node 22 and pnpm.

```bash
cp .env.example .env
make dev
```

That is PostgreSQL in a container, the API with hot reload, and the frontend dev
server. The app is at <http://localhost:5173>; `/api` is proxied to the backend,
so development is same-origin exactly like production — which is why Culina has
no CORS policy anywhere and no development-only authentication path.

`make` on its own lists every target.

## The conventions are not in a wiki

They are in [`.claude/skills/`](.claude/skills/), one directory per subject, and
they are enforced by the compiler, by architecture tests and by CI rather than
by review. Read the one that covers what you are touching before you touch it —
`dotnet-result-pattern` and `sveltekit-state-and-optimistic-ui` are the two most
worth reading first.

Some of what they enforce, so nothing is a surprise:

- **Warnings are errors**, in Debug as well as Release, with the full analyser
  set on. There is no separate lint step for the backend because the compile is
  one.
- **Layering is a test.** Domain and Contracts depend on nothing; Application
  depends on those; Infrastructure and Api may not be referenced by anything
  below them. `ArchitectureTests` fails a violation.
- **`Result` is observed only by `Match`.** There is no `IsSuccess` to branch on
  — which is what stops an error from being read and then ignored.
- **No colour outside the token system**, and no token that nothing declares.
  Both are tests: a stray `#fff` breaks re-theming invisibly, and a
  `var(--space-5)` that does not exist renders as nothing at all.
- **Nothing user-visible is hard-coded.** Every string is a message key, in
  German and English. Append to `messages/*.json`; never re-sort them, because
  the compiler falls over on some reorderings and can emit correct output *and*
  exit non-zero.

## Running the tests

```bash
make test        # backend and frontend unit tests
make lint        # formatting and lint rules, both halves
```

The end-to-end suite runs against the built app and a real backend, so it needs
an administrator account to exist:

```bash
CULINA_E2E_EMAIL=you@example.com CULINA_E2E_PASSWORD=... make test-e2e
```

Without those, the suites that need a backend say they were skipped rather than
passing quietly. Every flow makes an account of its own, named after its file.

The instance the suite runs against needs its rate limits raised, because six
browsers driving one instance from one address is a burst no person produces and
the defaults are right to refuse it. CI does this for the stack it starts; for a
local run, start the API with:

```bash
RateLimits__LoginPerIpPerMinute=1000 \
RateLimits__RegisterPerIpPerHour=1000 \
RateLimits__RequestsPerSessionPerMinute=20000 \
make backend
```

`make test-e2e` rebuilds the frontend, but uses the API that is already running.
Restart the API after endpoint changes. To test against a different local API,
set `CULINA_API=http://127.0.0.1:5001` alongside the test credentials.

The archive round-trip uses a fresh test household each run: importing into a
reused household would double its old recipes every time the suite runs.

## The contract between the halves

The frontend's API client is generated from the backend's OpenAPI document, and
CI fails if the two disagree. After changing anything an endpoint accepts or
returns:

```bash
make api     # export the document, regenerate the client
```

Commit both. A stale generated client cannot merge, which is what stops a
renamed field from becoming a runtime surprise weeks later in somebody's
kitchen.

## Work is tracked in beads

```bash
bd ready          # what is claimable, blockers resolved
bd show <id>      # the whole ticket before starting
bd update <id> --claim
```

A bead carries why the work exists, not just what to do. When one turns out to
be wrong — and several have — say so when closing it rather than implementing
something nobody wants.

## Commits

Say what changed and **why it is right**, in prose. The diff already says what
the code does; a commit message that repeats it has said nothing. If a change
fixes something, say what was broken and how it came to be that way — that is
the part nobody can reconstruct later.

## What a change should look like

Small, and finished. A defect comes with a test that fails without the fix —
verified to fail, not assumed to. A new behaviour comes with the message keys in
both languages. Anything touching the middleware pipeline comes with a test for
the property that depends on the order, because the order of that pipeline is a
contract and moving a line in it is a security change.

Everything carries the reason it is the way it is. A comment that says what the
next line does is noise; a comment that says why it is not the obvious
alternative is the only documentation that survives.
