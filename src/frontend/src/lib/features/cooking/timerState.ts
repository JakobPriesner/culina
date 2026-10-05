/** Shared, worker-safe storage. Transactions serialize page and notification actions. */
export interface KitchenTimer {
  readonly stepIndex: number;
  readonly endsAt: number;
  readonly label: string;
  readonly notified?: boolean;
  /** Seconds held while paused; the old deadline must not ring. */
  readonly pausedRemaining?: number;
}

export interface TimerState {
  initialized?: boolean;
  shoppingItems?: number;
  timers: KitchenTimer[];
  url: string;
  nextStep?: number;
}

export interface TimerNotice {
  type: 'culina:timer';
  sessionId: string;
  stepIndex: number;
  endsAt: number;
  url: string;
}

export const alarmCadence = [250, 100, 250, 100, 500];
const databaseName = 'culina-kitchen';
const storeName = 'state';

export function validTimers(value: unknown): KitchenTimer[] {
  if (!Array.isArray(value)) return [];
  return value.filter(
    (timer): timer is KitchenTimer =>
      timer !== null &&
      typeof timer === 'object' &&
      Number.isInteger(timer.stepIndex) &&
      timer.stepIndex >= 0 &&
      Number.isFinite(timer.endsAt) &&
      typeof timer.label === 'string' &&
      (timer.pausedRemaining === undefined ||
        (Number.isFinite(timer.pausedRemaining) && timer.pausedRemaining > 0))
  );
}

async function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(storeName);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
    request.onblocked = () => reject(new Error('Kitchen storage blocked'));
  });
}

/** null means unavailable; an empty record is still an authoritative answer. */
export async function editTimerState(
  sessionId: string,
  edit?: (state: TimerState) => TimerState
): Promise<TimerState | null> {
  let db: IDBDatabase | undefined;
  try {
    db = await open();
    return await new Promise((resolve, reject) => {
      const tx = db!.transaction(storeName, edit ? 'readwrite' : 'readonly');
      const store = tx.objectStore(storeName);
      const request = store.get(sessionId);
      let result: TimerState;
      request.onsuccess = () => {
        try {
          const stored = request.result as TimerState | undefined;
          result = { ...stored, timers: validTimers(stored?.timers), url: stored?.url ?? '/' };
          if (edit) {
            result = { ...edit(result), initialized: true };
            store.put(result, sessionId);
          }
        } catch (error) {
          tx.abort();
          reject(error);
        }
      };
      tx.oncomplete = () => resolve(result);
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error);
    });
  } catch {
    return null;
  } finally {
    db?.close();
  }
}

export async function applyTimerAction(notice: TimerNotice, action: string): Promise<boolean> {
  if (!['minute-1', 'minute-2', 'dismiss', 'next'].includes(action)) return false;
  let changed = false;
  const saved = await editTimerState(notice.sessionId, (state) => {
    const timer = state.timers.find(
      (t) => t.stepIndex === notice.stepIndex && t.endsAt === notice.endsAt
    );
    if (!timer || timer.pausedRemaining !== undefined) return state;
    changed = true;
    state.timers = state.timers.filter((t) => t !== timer);
    if (action.startsWith('minute-')) {
      state.timers.push({
        stepIndex: timer.stepIndex,
        label: timer.label,
        endsAt: Date.now() + (action === 'minute-1' ? 60_000 : 120_000)
      });
    } else if (action === 'next') {
      state.nextStep = timer.stepIndex + 1;
    }
    return state;
  });
  return saved !== null && changed;
}

export async function forgetKitchen(): Promise<void> {
  let db: IDBDatabase | undefined;
  try {
    db = await open();
    await new Promise<void>((resolve, reject) => {
      const tx = db!.transaction(storeName, 'readwrite');
      tx.objectStore(storeName).clear();
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    // Device storage is optional.
  } finally {
    db?.close();
  }
}
