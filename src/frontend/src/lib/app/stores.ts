/**
 * How far a store has got with the read it was asked for.
 *
 * One type rather than one per store. These four states meant the same thing in
 * all nine places that spelled them out — under four different names, which is
 * how a tenth store ends up inventing a fifth state nobody renders.
 */
export type LoadStatus = 'idle' | 'loading' | 'ready' | 'failed';

/**
 * Every store that holds something belonging to the signed-in person.
 *
 * Signing out has to clear all of them. Stale data from a previous user is a
 * security bug, not a glitch: on a shared tablet in a kitchen, the next person
 * to sign in must not see the last one's recipes for even one frame.
 *
 * A registry rather than a list of imports in the sign-out function, because
 * that list is the thing that gets forgotten when a store is added.
 */
const resets = new Set<() => void>();

/** Called once by each store, at module scope. */
export function registerStore(reset: () => void): void {
  resets.add(reset);
}

/** Called on sign-out, on an expired session, and between tests. */
export function resetAllStores(): void {
  for (const reset of resets) {
    reset();
  }
}
