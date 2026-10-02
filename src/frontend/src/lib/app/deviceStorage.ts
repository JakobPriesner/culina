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

/**
 * Removes every `<prefix><userId>…` key on this device, except `keep`'s.
 *
 * For what is remembered per account — unsent drafts, the last recipe started,
 * recent searches — on a device more than one person uses.
 */
export function forgetAccountKeys(prefix: string, keep?: string): void {
  try {
    for (const key of Object.keys(globalThis.localStorage ?? {})) {
      const owner = key.slice(prefix.length).split('.')[0];

      if (key.startsWith(prefix) && owner !== keep) {
        globalThis.localStorage.removeItem(key);
      }
    }
  } catch {
    // Nothing to do: a store that cannot be read holds nothing to remove.
  }
}
