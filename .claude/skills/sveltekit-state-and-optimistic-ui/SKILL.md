---
name: sveltekit-state-and-optimistic-ui
description: Client state conventions for the culina-v2 SvelteKit app — local per-feature rune stores as the single source of truth, optimistic updates with snapshot rollback, and disciplined error handling for 401/403/409/412/network failures. Use when adding client state, wiring a mutation, or handling an API failure in the UI.
---

# Local stores and optimistic UI

**Every feature owns one local store; the store is the only thing components
read domain data from.** No global god-store, no cross-feature imports of
another feature's state, no data duplicated between a store and component
state.

## The store

One file per domain, `lib/features/<domain>/stores/<domain>.svelte.ts`, a class
instantiated once and exported:

```ts
import { api } from '$lib/api/client';
import type { Recipe } from '../types';

class RecipeStore {
  items = $state<Recipe[]>([]);
  status = $state<'idle' | 'loading' | 'ready' | 'error'>('idle');
  error = $state<AppError | null>(null);

  async load() {
    this.status = 'loading';
    const result = await api.recipes.list();
    result.match(
      (page) => { this.items = page.items; this.status = 'ready'; this.error = null; },
      (error) => { this.error = error; this.status = 'error'; },
    );
  }
}

export const recipes = new RecipeStore();
```

- `$state` fields, plain async methods. No `writable`/`derived` stores, no
  `get(store)`, no manual subscriptions.
- The store holds **normalised domain objects** mapped from the generated API
  types, each keeping its `version` for `If-Match` (`http-caching-etags`).
- Derived views (`filtered`, `byId`) are `$derived` in the store or the
  component — never a second copy of the data kept in sync by hand.
- Server state is fetched, not invented: after a mutation the store trusts the
  response body it gets back rather than re-fetching the whole list.
- Clear every store on logout. Stale data from a previous user is a security
  bug, not a glitch.

## Optimistic updates

The user's action should feel instant. The store applies the change locally,
sends the request, and either keeps the server's answer or rolls back to the
exact previous value.

```ts
async rename(id: string, title: string) {
  const index = this.items.findIndex((item) => item.id === id);
  if (index < 0) return;

  const previous = this.items[index];            // snapshot: the whole object
  this.items[index] = { ...previous, title, pending: true };

  const result = await api.recipes.update(id, { title }, previous.version);

  result.match(
    (updated) => { this.items[index] = updated; },       // server's version wins
    (error) => {
      this.items[index] = previous;                      // exact rollback
      toast.error(messageFor(error));
    },
  );
}
```

Rules:

- **Snapshot before mutating**, and restore that snapshot on failure. Never
  "undo" by applying an inverse operation — inverses drift.
- Mark optimistic rows (`pending: true`) so the UI can dim them; do not block
  the whole screen for an optimistic action.
- Optimistic is for **small, likely-to-succeed, easily-reversible** changes:
  rename, toggle, reorder, add-to-list, delete-with-undo. Not for payments, not
  for anything whose failure the user could not tolerate seeing reversed, not
  for multi-step server workflows — those show a pending state and wait.
- A created entity gets a temporary client id, replaced by the server's id in
  the success branch. Nothing else may persist a temporary id.
- Serialise mutations per entity (a small in-flight map keyed by id) so two
  quick edits cannot land out of order, and later actions on a `pending` row
  either queue or are disabled.

## Error handling

Every API call returns a result; there are no unhandled rejections and no
`catch` that only logs. Map the error's `code`/status to one of four
behaviours:

| Failure | UI |
| --- | --- |
| `401` | Clear auth + stores, redirect to login, preserve the intended URL. Never a silent retry loop. |
| `403` | Inline "you don't have access" — no redirect, no retry. (`auth.csrf_invalid`: refresh token, retry **once**.) |
| `404` | Route-level empty state ("this recipe no longer exists"), remove it from the store. |
| `409` / `412` | Roll back the optimistic change and tell the user it changed elsewhere, with a reload action. Never retry blindly. |
| `422`/`400` validation | Map `errors[]` onto the form fields by name; focus the first one. Never a generic toast for a field error. |
| `429` | Disable the action and show when to try again, from the `Retry-After` header. |
| Network / `5xx` | Keep the data on screen, show a non-blocking retry affordance. Retry idempotent `GET`s once with backoff; never auto-retry an unsafe request. |

Always show the problem document's `requestId` in the detail of an unexpected
error — that is what makes a user report traceable to a log line
(`dotnet-observability`).

Never: `alert()`, a raw exception message in the UI, an empty screen with no
explanation, or swallowing an error because "it usually works".

## Persistence

- Domain data lives in memory only. `localStorage` is for UI preferences
  (theme, collapsed panels, last-used filter) and never for anything private or
  anything the server owns.
- The URL is state too: filters, sort and pagination belong in query
  parameters, so a page can be shared and reloaded into the same view.

## Checklist

- [ ] One store per feature, `$state` fields, no cross-feature imports.
- [ ] Components read domain data only from the store; no duplicated copies.
- [ ] Mutations snapshot, apply optimistically, and restore exactly on failure.
- [ ] Optimistic only where reversal is safe; heavy operations show pending.
- [ ] Every failure mapped to a deliberate UI outcome from the table.
- [ ] Stores cleared on logout; nothing private in `localStorage`.
