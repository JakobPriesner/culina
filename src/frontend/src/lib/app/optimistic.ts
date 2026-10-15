/**
 * What a failed optimistic write may undo. Counts the store's resets, so an undo that lands after sign-out (the 401 that
 * cleared the store comes first) does nothing instead of refilling the cleared store with the signed-out person's data.
 */
export class ResetEpoch {
  #resets = 0;

  /** Call from the store's reset(). */
  bump(): void {
    this.#resets++;
  }

  /** Wraps an undo so it is skipped once the store was reset after this call. */
  guard(undo: () => void): () => void {
    const mine = this.#resets;

    return () => {
      if (mine === this.#resets) {
        undo();
      }
    };
  }
}

/** Removes one element and returns what puts just it back at its old place (not if it is already back); writes that landed meanwhile stay. */
export function removeUndoable<T>(
  items: readonly T[],
  isIt: (item: T) => boolean
): { items: T[]; undo: (current: readonly T[]) => T[] } {
  const index = items.findIndex(isIt);
  const removed = items[index];

  if (index < 0 || removed === undefined) {
    return { items: [...items], undo: (current) => [...current] };
  }

  return {
    items: items.filter((_, at) => at !== index),
    undo: (current) =>
      current.some(isIt)
        ? [...current]
        : [...current.slice(0, index), removed, ...current.slice(index)]
  };
}
