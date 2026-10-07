/**
 * One in-flight write per key.
 *
 * Two quick edits to the same recipe must not race: the second waits for the
 * first, so the version it sends is the one the first produced.
 */
export class WriteQueue {
  #writes = new Map<string, Promise<unknown>>();

  /** Runs `write` after whatever is already queued for `key`, failed or not. */
  async run<TResult>(key: string, write: () => Promise<TResult>): Promise<TResult> {
    const queued = (this.#writes.get(key) ?? Promise.resolve()).then(write, write);

    this.#writes.set(key, queued);

    try {
      return await queued;
    } finally {
      if (this.#writes.get(key) === queued) {
        this.#writes.delete(key);
      }
    }
  }

  clear(): void {
    this.#writes.clear();
  }
}
