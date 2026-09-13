# Security

## Reporting a vulnerability

Open a [private security advisory][advisory] on the repository. That reaches the
maintainer and nobody else, and it is the only channel worth using — a public
issue tells everybody running an instance about the hole at the same moment it
tells the person who can fix it.

[advisory]: https://github.com/jakobpriesner/culina/security/advisories/new

Please include enough to reproduce it: the version (`docker inspect` shows
`org.opencontainers.image.revision` on the running image), what you did, and
what happened. A proof of concept is welcome and never required.

**What you can expect.** An acknowledgement within three days. An assessment
within a week, saying plainly whether it is a vulnerability, what it affects and
when a fix is likely. A fix released with an advisory naming you, unless you
would rather not be named.

**What is asked of you.** Time to fix it before it is public — ninety days is
the usual figure, and something actively exploited moves a great deal faster.
Nothing that harms somebody else's instance: no testing against an instance you
do not run, no denial of service, no accessing data that is not yours. There is
no bounty; this is a recipe app written by one person.

Each instance publishes `/.well-known/security.txt` with its own operator's
contact, when its operator has configured one. For a vulnerability in Culina
itself, use the advisory link above.

## What is in scope

The application, the container image, and the deployment the compose files
describe. Of particular interest:

- Anything that lets one household see another's recipes, shopping list or
  photographs.
- Anything that lets a personal note reach somebody it was not written for.
- Authentication and session handling: the cookie, the CSRF token, the
  same-origin guard, and anything that survives a sign-out.
- The service worker's cache, which is allowed to hold recipes on a device and
  is required to empty itself when anybody signs in or out.
- Anything that turns an uploaded photograph into something other than a
  photograph.

## What is not

Missing hardening headers on an instance whose operator has not configured a
proxy — TLS, HSTS and `X-Forwarded-*` are documented as the proxy's job.
Rate-limit thresholds, which are configuration. Vulnerabilities in a dependency
already known and already flagged by CI, unless you can show they are actually
reachable. Anything requiring a compromised administrator account, which is not
a vulnerability but a consequence.

## How the app is built to make this smaller

- Sessions are opaque references in an `HttpOnly`, `SameSite=Lax`, `__Host-`
  prefixed cookie. Nothing about a person is in the cookie itself.
- Every unsafe request is checked twice: it must come from this origin, and it
  must carry the CSRF token from a companion cookie.
- Passwords are Argon2id and are rehashed on sign-in when the cost settings rise.
- The content security policy carries a per-response nonce; there is no
  `unsafe-inline` anywhere.
- The image ships with no shell and no package manager, runs as a non-root user
  on a read-only filesystem with every capability dropped.
- Restores are locked, images are scanned on every change and again nightly
  after release, and a vulnerable dependency fails the compile rather than a
  review.

None of that is a guarantee. It is why a report is worth making: the interesting
holes are the ones none of it anticipated.
