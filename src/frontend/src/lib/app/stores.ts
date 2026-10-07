/** How far a store has got with its read; one shared type so a new store does not invent a fifth state. */
export type LoadStatus = 'idle' | 'loading' | 'ready' | 'failed';

/**
 * Every store holding the signed-in person's data, cleared on sign-out (stale data on a shared tablet is a security bug).
 * A registry rather than imports in the sign-out function, since that list is what gets forgotten.
 */
const resets = new Set<() => void>();

export function registerStore(reset: () => void): void {
  resets.add(reset);
}

/** Called on sign-out, on an expired session, and between tests. */
export function resetAllStores(): void {
  for (const reset of resets) {
    reset();
  }
}
