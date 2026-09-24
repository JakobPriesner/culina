---
name: cookie-auth-and-security
description: Cookie-based authentication and the required HTTP security headers for culina-v2, backend and frontend — session cookie attributes, CSRF synchronizer tokens, same-origin enforcement, the full security header set with CSP, and what the SvelteKit client must and must not do. Use when touching login/logout/session code, cookies, CSRF, CORS, headers, or any authenticated fetch from the frontend.
---

# Cookie auth and security headers

**Authentication is a cookie, never a token in JavaScript.** No JWT in
`localStorage`, no access token in a store, no `Authorization` header from the
browser. A cookie the script cannot read is the only credential a cross-site
script cannot steal.

## The session cookie

```csharp
options.Cookie.Name        = "__Host-culina.session";
options.Cookie.HttpOnly    = true;       // unreadable from JavaScript
options.Cookie.Secure      = true;       // HTTPS only (also required by __Host-)
options.Cookie.SameSite    = SameSiteMode.Lax;  // survives top-level navigation, blocks cross-site POST
options.Cookie.Path        = "/";        // required by the __Host- prefix
options.Cookie.MaxAge      = TimeSpan.FromDays(30);
options.SlidingExpiration  = true;
```

- The `__Host-` prefix pins the cookie to this exact origin: no `Domain`
  attribute, `Secure`, `Path=/`. A subdomain cannot set or overwrite it.
- The cookie carries an **opaque session id**, not claims. The session row in
  the database is the truth, so logout, revocation and privilege changes take
  effect immediately. A self-contained token cannot be revoked.
- Session records hold: user id, created/last-seen, IP and user agent (for the
  "your sessions" screen), CSRF token digest, absolute expiry.
- On login: create a new session **and** issue a new cookie value (session
  fixation). On logout: delete the server-side session, then expire the cookie.
- Data-protection keys must live on a persisted volume; without that every
  restart silently signs everyone out.

## CSRF: a real per-session token

`SameSite=Lax` and an origin check are necessary but not sufficient; the CSRF
defence is a **synchronizer token**.

- On session creation the server generates a random token, stores its digest on
  the session, and returns the token to the client (in a readable, non-`HttpOnly`
  companion cookie or in the login/session response body).
- Every **unsafe** cookie-authenticated request (`POST`, `PUT`, `PATCH`,
  `DELETE`) must send it back in `X-Culina-CSRF`. `CsrfMiddleware` compares it
  against the session's digest in constant time and rejects mismatches with
  `403` and code `auth.csrf_invalid`.
- `SameOriginMiddleware` additionally requires `Origin` (or `Referer`) on unsafe
  requests to match the app's own origin — checked before CSRF, so a foreign
  origin never reaches the token comparison.
- `GET`/`HEAD`/`OPTIONS` are exempt, which is only safe because **no `GET`
  endpoint changes state** (`rest-api-design`).
- Rotate the CSRF token on login and on privilege change.

## No CORS

The SPA is served from the same origin as the API. There is no CORS policy,
and none is added "for local development" — the dev server proxies `/api` to
the backend instead. A non-browser client (a desktop shell) authenticates with
a bearer token on a separate path and is exempt from the cookie/CSRF rules,
never by loosening them.

## Required response headers

Set in exactly one place, `SecurityHeadersMiddleware`, for every response:

| Header | Value | Why |
| --- | --- | --- |
| `Content-Security-Policy` | see below | The main defence against XSS. |
| `X-Content-Type-Options` | `nosniff` | Stops MIME sniffing into script. |
| `X-Frame-Options` | `DENY` | Legacy clickjacking defence next to CSP. |
| `Referrer-Policy` | `no-referrer` | Ids in paths never leak to third parties. |
| `Cross-Origin-Opener-Policy` | `same-origin` | Isolates the browsing context. |
| `Cross-Origin-Resource-Policy` | `same-origin` | Blocks cross-origin embedding of our responses. |
| `Permissions-Policy` | `camera=(self), microphone=(), geolocation=()` | Least privilege for device APIs; list only what is used. |
| `Cache-Control` | `no-store` on authenticated responses | Keeps private data out of shared caches. |

CSP differs by what is being served:

- **API responses**: `default-src 'none'; frame-ancestors 'none'; base-uri
  'none'; form-action 'none'` — a JSON response needs nothing.
- **The SPA document**: `default-src 'self'; script-src 'self' 'nonce-…';
  style-src 'self' 'nonce-…'; style-src-attr 'unsafe-hashes' 'sha256-…';
  img-src 'self' data: blob:; connect-src 'self'; font-src 'self';
  object-src 'none'; base-uri 'none'; frame-ancestors 'none';
  form-action 'self'`. The single `style-src-attr` hash is SvelteKit's route
  announcer (`SecurityHeaders.AnnouncerStyleHash`); a nonce cannot go on an
  attribute, so no other `style="…"` may reach the document — set styles
  through the CSSOM (Svelte's `style:` directive) or a nonced `<style>`.

No `'unsafe-inline'` and no `'unsafe-eval'` in `script-src`, ever — they
disable the policy. Inline scripts carry a per-response nonce.

**No HSTS, no HTTPS redirect, no response compression in the app.** TLS
terminates at the operator's reverse proxy, which owns HSTS; compressing
cookie-authenticated responses invites BREACH.

## Rate limiting and account safety

- Login, password reset and registration are rate limited per IP **and** per
  account, before authentication runs.
- Failed login says only "invalid credentials" — never which half was wrong,
  and never whether the account exists.
- Passwords are hashed with a memory-hard algorithm (Argon2id or scrypt) with
  parameters in configuration; the hash is never logged.
- Every rejection (bad credentials, CSRF, origin, rate limit) is a `Warning`
  log plus a counter (`dotnet-observability`).

## The frontend side

- `fetch` always with `credentials: 'include'` (or `'same-origin'`); the client
  never reads, stores, or attaches the session cookie itself.
- The API client attaches `X-Culina-CSRF` to every unsafe request from the
  readable CSRF cookie — in **one** place, the generated-client wrapper
  (`frontend-api-client`), never per call site.
- A `401` clears local auth state and routes to login; it never triggers a
  silent retry loop. A `403` with code `auth.csrf_invalid` refreshes the CSRF
  token once, then retries once, then gives up.
- No credential, id token or user object is persisted to `localStorage` or
  `sessionStorage`. The server is the source of truth; the client keeps the
  current user in memory and re-fetches `/users/me` on load.
- Never render server-provided HTML without sanitising it, and never build
  markup with `{@html}` from user content.
- The SPA build must not need `'unsafe-inline'`. If a dependency demands it,
  replace the dependency.

## Checklist

- [ ] Cookie is `__Host-` prefixed, `HttpOnly`, `Secure`, `SameSite=Lax`,
      opaque, server-side revocable; reissued on login.
- [ ] Unsafe requests require both an allowed `Origin` and a valid per-session
      CSRF token.
- [ ] No `GET` endpoint mutates state.
- [ ] Every header in the table is set for every response; CSP has no
      `unsafe-inline`/`unsafe-eval`.
- [ ] Authenticated responses are `no-store`.
- [ ] Frontend sends credentials + CSRF from one wrapper and stores no
      credential anywhere.
- [ ] Integration tests cover each rejection path.
