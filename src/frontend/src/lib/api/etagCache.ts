/**
 * Remembers the last response for a URL, so a re-read costs a 304 instead of a
 * payload and so a write can carry the version it was based on.
 *
 * Memory only, never storage: this holds a household's recipes and a person's
 * notes, and private data must not outlive the session on a shared machine.
 */
interface CachedResponse {
  readonly etag: string;
  readonly body: string;
}

const entries = new Map<string, CachedResponse>();

export function remember(url: string, etag: string, body: string): void {
  entries.set(url, { etag, body });
}

export function cached(url: string): CachedResponse | undefined {
  return entries.get(url);
}

/**
 * Forgets what a write to this URL could have changed.
 *
 * A write to `/recipes/x` affects the recipe, the list it appears in, and
 * anything hanging off it, so the rule is "this URL and anything on its path".
 * Wider than strictly necessary and far cheaper than being wrong.
 */
export function invalidate(url: string): void {
  const target = pathOf(url);

  for (const key of [...entries.keys()]) {
    const candidate = pathOf(key);

    if (target.startsWith(candidate) || candidate.startsWith(target)) {
      entries.delete(key);
    }
  }
}

/** Called on sign-out: nothing read as one person may be served to the next. */
export function forgetEverything(): void {
  entries.clear();
}

const pathOf = (url: string) => new URL(url, 'http://cache.invalid').pathname;
