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
