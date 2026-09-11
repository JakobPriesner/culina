---
name: frontend-api-client
description: How the culina-v2 frontend talks to the API — types generated from the backend's OpenAPI document with openapi-typescript, a single typed openapi-fetch wrapper that owns credentials, CSRF, ETags and error mapping, and the rule that generated code is never edited or imported directly by components. Use when regenerating the client, adding an API call, or changing fetch behaviour.
---

# The generated API client

**The OpenAPI document is the contract, and the client is generated from it.**
Nobody hand-writes a request type, a response type, or a URL string. When the
backend changes, regeneration makes the frontend fail to compile — which is the
point.

## Generation

```jsonc
// package.json
"scripts": {
  "generate:api": "openapi-typescript ../backend/openapi/Api.json --output src/lib/api/generated/schema.d.ts",
  "check": "svelte-kit sync && svelte-check --tsconfig ./tsconfig.json"
}
```

- The backend exports its OpenAPI document at build time; the frontend
  generates from **that file**, not from a running server.
- Output goes to `src/lib/api/generated/` — committed, never hand-edited,
  ignored by the formatter and linter, and the first thing to check when types
  look wrong. A `// generated — do not edit` banner sits at the top.
- Regeneration is part of the normal flow: change the backend contract →
  `pnpm generate:api` → fix the compile errors → commit both.
- CI regenerates and fails if the result differs from what is committed, so a
  stale client cannot merge.

## The wrapper is the only thing components see

`openapi-fetch` gives a typed client over the generated schema. One module
configures it, and everything cross-cutting lives there — once
(`code-simplicity`):

```ts
// src/lib/api/client.ts
import createClient from 'openapi-fetch';
import type { paths } from './generated/schema';

const raw = createClient<paths>({
  baseUrl: '/api',
  credentials: 'include',          // cookie auth — never a token header
});

raw.use({
  onRequest({ request }) {
    if (!SAFE_METHODS.has(request.method)) {
      request.headers.set('X-Culina-CSRF', readCsrfToken());
    }
    return request;
  },
  onResponse({ response }) {
    recordEtag(response);          // http-caching-etags
    return response;
  },
});
```

The wrapper owns, in one place:

- `credentials: 'include'` and the CSRF header on unsafe methods
  (`cookie-auth-and-security`).
- The ETag cache: `If-None-Match` on reads, `304` served from memory,
  `If-Match` from the entity's `version` on writes (`http-caching-etags`).
- Turning every response into a **result**, never a thrown exception:
  `Ok<T> | Err<AppError>`, where `AppError` is parsed from the RFC 9457 problem
  document (`code`, `detail`, `errors[]`, `requestId`). The UI branches on
  `code`, not on a message string.
- The global `401` handling: clear auth state and stores, route to login.
- Request-level timeout and `AbortSignal` plumbing, so every call is
  cancellable when a component unmounts or a route changes.

Components and stores import **`api`**, never `generated/schema` and never
`fetch`. Feature stores expose domain methods (`recipes.list()`) that call the
wrapper and map generated types into the feature's own UI types.

## Typing discipline

```ts
import type { components } from '$lib/api/generated/schema';

type RecipeResponse = components['schemas']['RecipesGetResponse'];
```

- Derive types from the generated schema; never redeclare a shape by hand.
- Never `as` a response into a hoped-for type, and never `any`. A cast around
  the generated types means the contract is wrong — fix the backend.
- Map generated types to feature types at the store boundary, so a rename on
  the wire touches one mapper instead of every component — the frontend mirror
  of `dotnet-layer-mapping`.

## SvelteKit specifics

- Route `load` functions call the wrapper, passing SvelteKit's `fetch` so SSR
  and request-scoped cookies work.
- This app ships as a static SPA build, so no server-side secret ever reaches
  the client: everything the client can see is public by definition.
- Keep the dev proxy pointing `/api` at the backend so the app is same-origin
  in development too; that keeps cookies and CSRF behaving exactly as in
  production and is why there is no CORS policy anywhere.

## Checklist

- [ ] Types come from `openapi-typescript`; generated output committed,
      unedited, and regenerated in CI.
- [ ] Every call goes through `lib/api/client.ts`; no bare `fetch`, no direct
      import of the generated schema outside `lib/api` and feature mappers.
- [ ] Credentials, CSRF, ETags, error mapping, `401` handling, timeouts and
      cancellation are implemented once, in the wrapper.
- [ ] Calls return results; nothing throws on a `4xx`.
- [ ] Generated types are mapped into feature types at the store boundary.
