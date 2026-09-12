/**
 * Module-level state is shared by every test in a file, so one test that logs
 * in leaks into the next one that expects to be logged out. A store registers
 * its reset here and the test setup calls all of them between tests, which
 * means nobody has to remember.
 *
 * Registration is a no-op outside tests, so this costs a function reference in
 * the bundle and nothing else.
 */
const resets = new Set<() => void>();

/** Lets a store put itself back to its initial state between tests. */
export function registerReset(reset: () => void): void {
  resets.add(reset);
}

/** Called by the test setup. Not meant for individual tests. */
export function resetAll(): void {
  for (const reset of resets) {
    reset();
  }
}
