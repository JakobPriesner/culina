/** One in-flight write per key: a second edit waits so it sends the version the first produced. */
export class WriteQueue {
  #writes = new Map<string, Promise<unknown>>();

  /** Runs `write` after the key's queue, even if an earlier write failed. */
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
