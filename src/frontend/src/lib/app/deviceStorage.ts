/** What this device remembers between visits. Storage throws in private windows or with site data blocked; every caller has an answer for "nothing remembered". */
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

/** Removes every `<prefix><userId>…` key except `keep`'s: per-account memories (drafts, last recipe, recent searches) on a shared device. */
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
