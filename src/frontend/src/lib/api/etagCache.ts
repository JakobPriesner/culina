/** Last response per URL, for 304 re-reads and write versions. Memory only: private data must not outlive the session. */
interface CachedResponse {
  readonly etag: string;
  readonly body: string;
  readonly path: string;
}

const capacity = 100;

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

/** Forgets this URL and anything on its path (the recipe, its list, its children); deliberately wide. */
export function invalidate(url: string): void {
  const target = pathOf(url);

  for (const [key, { path }] of [...entries]) {
    if (target.startsWith(path) || path.startsWith(target)) {
      entries.delete(key);
    }
  }
}

/** On sign-out, so nothing read as one person is served to the next. */
export function forgetEverything(): void {
  entries.clear();
}

const pathOf = (url: string) => new URL(url, 'http://cache.invalid').pathname;
