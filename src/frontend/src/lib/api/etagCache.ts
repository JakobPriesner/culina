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
  /** Parsed once when remembered, so `invalidate` does not parse every key. */
  readonly path: string;
}

/** Enough for the pages in one sitting; the oldest-read entry goes first. */
const capacity = 100;

/** Insertion order is read order: a hit moves its entry to the end. */
const entries = new Map<string, CachedResponse>();

export function remember(url: string, etag: string, body: string): void {
  entries.delete(url);
  entries.set(url, { etag, body, path: pathOf(url) });

  if (entries.size > capacity) {
    const oldest = entries.keys().next().value;

    if (oldest !== undefined) {
      entries.delete(oldest);
    }
  }
}

export function cached(url: string): CachedResponse | undefined {
  const entry = entries.get(url);

  if (entry) {
    entries.delete(url);
    entries.set(url, entry);
  }

  return entry;
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

  for (const [key, { path }] of [...entries]) {
    if (target.startsWith(path) || path.startsWith(target)) {
      entries.delete(key);
    }
  }
}

/** Called on sign-out: nothing read as one person may be served to the next. */
export function forgetEverything(): void {
  entries.clear();
}

const pathOf = (url: string) => new URL(url, 'http://cache.invalid').pathname;
