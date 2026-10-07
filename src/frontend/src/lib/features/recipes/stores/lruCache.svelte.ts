/*
 * A plain Map on purpose: recency is insertion order, which `get` changes during render,
 * a write a SvelteMap would reject. Reactivity is the `#version` counter instead.
 */
/* eslint-disable svelte/prefer-svelte-reactivity */

/**
 * Reactive map of at most `limit` entries, dropping the least recently used. `get` counts as use;
 * `peek` neither tracks nor counts, for code about to write the key.
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

  update(change: (value: TValue) => TValue): void {
    for (const [key, value] of this.#entries) {
      this.#entries.set(key, change(value));
    }

    this.#version += 1;
  }

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
