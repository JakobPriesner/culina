/*
 * A plain Map on purpose: recency is its insertion order, which `get` has to
 * change while a component is rendering — a write a SvelteMap would reject.
 * Reactivity is the `#version` counter instead.
 */
/* eslint-disable svelte/prefer-svelte-reactivity */

/**
 * A reactive map that holds at most `limit` entries, dropping the one used
 * least recently.
 *
 * The entries live in a plain `Map`, whose insertion order is the recency
 * order, and one `$state` counter says when they changed. A reactive record
 * would copy itself on every write; this copies nothing, and a reader that
 * called `get` is told to run again whenever any entry is written.
 *
 * `get` counts as use, so what is on screen is never what gets dropped.
 * `peek` reads without either — for code that is about to write the key, and
 * must not make an effect depend on it.
 */
export class LruCache<TValue> {
  #entries = new Map<string, TValue>();
  #version = $state(0);
  #limit: number;
  #evicted: (key: string) => void;

  constructor(limit: number, evicted: (key: string) => void = () => {}) {
    this.#limit = limit;
    this.#evicted = evicted;
  }

  get size(): number {
    void this.#version;

    return this.#entries.size;
  }

  /** The entry, tracked, and now the most recently used. */
  get(key: string): TValue | undefined {
    void this.#version;

    const value = this.#entries.get(key);

    if (value !== undefined) {
      this.#entries.delete(key);
      this.#entries.set(key, value);
    }

    return value;
  }

  /** The entry, untracked and without counting as use. */
  peek(key: string): TValue | undefined {
    return this.#entries.get(key);
  }

  set(key: string, value: TValue): void {
    this.#entries.delete(key);
    this.#entries.set(key, value);

    while (this.#entries.size > this.#limit) {
      const oldest = this.#entries.keys().next().value as string;

      this.#entries.delete(oldest);
      this.#evicted(oldest);
    }

    this.#version += 1;
  }

  /** Replaces every entry with `change` of it, keeping the order. */
  update(change: (value: TValue) => TValue): void {
    for (const [key, value] of this.#entries) {
      this.#entries.set(key, change(value));
    }

    this.#version += 1;
  }

  /** What `restore` puts back exactly as it was. */
  snapshot(): Map<string, TValue> {
    return new Map(this.#entries);
  }

  restore(snapshot: Map<string, TValue>): void {
    this.#entries = new Map(snapshot);
    this.#version += 1;
  }

  clear(): void {
    this.#entries.clear();
    this.#version += 1;
  }
}
