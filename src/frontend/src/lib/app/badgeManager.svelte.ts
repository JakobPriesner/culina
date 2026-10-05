export interface BadgeApi {
  setAppBadge?: (count: number) => Promise<void>;
  clearAppBadge?: () => Promise<void>;
}

/** Serialized, coalesced writes: an old request can never land after a newer clear. */
export function createBadgeManager(api: () => BadgeApi = () => navigator) {
  let desired = 0;
  let applied: number | undefined;
  let sending = false;
  async function flush() {
    if (sending) return;
    sending = true;
    try {
      while (applied !== desired) {
        const count = desired;
        try {
          const target = api();
          if (count > 0) await target.setAppBadge?.(count);
          else await target.clearAppBadge?.();
        } catch {
          // Unsupported, denied, or not installed: no interruption.
        }
        applied = count;
      }
    } finally {
      sending = false;
    }
  }
  return {
    update(runningTimers: number, shoppingItems: number) {
      desired = Math.max(0, runningTimers > 0 ? runningTimers : shoppingItems);
      void flush();
    },
    clear() {
      desired = 0;
      void flush();
    }
  };
}

export const badgeManager = createBadgeManager();
