/**
 * What this device remembers between visits.
 *
 * Storage throws in a private window and in a browser set to block site data,
 * and nothing kept here is worth failing over: every caller has a sensible
 * answer for "nothing remembered".
 */
export function readDevice(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

export function writeDevice(key: string, value: string): void {
  try {
    globalThis.localStorage?.setItem(key, value);
  } catch {
    // Nothing to do: it simply will not survive a reload.
  }
}
