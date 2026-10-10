import { SvelteSet } from 'svelte/reactivity';

import { haptics } from '$shell/haptics';
import { playKitchenChime, unlockAudio } from './kitchenAudio';
import {
  closeTimerNotification,
  notifyTimerDone,
  requestTimerNotificationPermission
} from './timerNotification';
import { editTimerState, validTimers, type KitchenTimer, type TimerState } from './timerState';
export type { KitchenTimer } from './timerState';

const storageKey = (id: string) => `culina.timers.${id}`;

export function createTimers(sessionId: () => string | null, url: () => string = () => '/') {
  let timers = $state<KitchenTimer[]>([]);
  let now = $state(Date.now());
  let nextStep = $state<number | undefined>();
  let loadedFor: string | null = null;
  let revision = 0;
  let pending = Promise.resolve();
  const alerted = new SvelteSet<string>();

  /** One session at a time: another session's key was left by one that ended unseen. */
  function sweepOtherSessions(keep: string) {
    try {
      const stale = Object.keys(localStorage).filter(
        (key) => key.startsWith(storageKey('')) && key !== storageKey(keep)
      );
      stale.forEach((key) => localStorage.removeItem(key));
    } catch {
      /* Optional. */
    }
  }

  function cache(id: string) {
    try {
      localStorage.setItem(storageKey(id), JSON.stringify(timers));
    } catch {
      /* Optional. */
    }
  }

  function queue(id: string, edit: (state: TimerState) => TimerState, adopt = false) {
    const version = ++revision;
    pending = pending.then(async () => {
      const state = await editTimerState(id, edit);
      if (adopt && state && loadedFor === id && version === revision) {
        timers = state.timers;
        nextStep = state.nextStep;
        cache(id);
      }
    });
    return pending;
  }

  async function refresh() {
    const id = loadedFor;
    const version = revision;
    if (!id) return;
    await pending;
    const state = await editTimerState(id);
    if (state?.initialized && loadedFor === id && version === revision) {
      timers = state.timers;
      nextStep = state.nextStep;
      cache(id);
    }
  }

  function checkAlarms() {
    const id = loadedFor;
    if (!id) return;
    for (const timer of timers) {
      const key = `${id}:${timer.stepIndex}:${timer.endsAt}`;
      if (
        timer.pausedRemaining !== undefined ||
        timer.endsAt > now ||
        timer.notified ||
        alerted.has(key)
      )
        continue;
      alerted.add(key);
      const href = url();
      pending = pending.then(async () => {
        let claimed = false;
        const state = await editTimerState(id, (stored) => {
          stored.timers = stored.timers.map((t) => {
            if (
              t.stepIndex !== timer.stepIndex ||
              t.endsAt !== timer.endsAt ||
              t.notified ||
              t.pausedRemaining !== undefined
            )
              return t;
            claimed = true;
            return { ...t, notified: true };
          });
          return stored;
        });
        if (
          loadedFor !== id ||
          !timers.some(
            (t) =>
              t.stepIndex === timer.stepIndex &&
              t.endsAt === timer.endsAt &&
              t.pausedRemaining === undefined
          )
        )
          return;
        if (!state || claimed) {
          playKitchenChime();
          // OS notification vibration handles hidden windows.
          if (document.visibilityState === 'visible') haptics.alarm();
          await notifyTimerDone(timer.label, {
            type: 'culina:timer',
            sessionId: id,
            stepIndex: timer.stepIndex,
            endsAt: timer.endsAt,
            url: href
          });
        }
      });
    }
  }

  const remaining = (timer: KitchenTimer) =>
    timer.pausedRemaining ?? Math.max(0, Math.ceil((timer.endsAt - now) / 1000));
  return {
    get timers() {
      return timers;
    },
    get runningCount() {
      return timers.filter((t) => t.pausedRemaining === undefined && t.endsAt > now).length;
    },
    get nextStep() {
      return nextStep;
    },
    remaining,
    isDone: (timer: KitchenTimer) => timer.pausedRemaining === undefined && timer.endsAt <= now,
    load() {
      const id = sessionId();
      if (id === loadedFor) return;
      loadedFor = id;
      revision++;
      alerted.clear();
      nextStep = undefined;
      now = Date.now();
      timers = [];
      if (!id) return;
      sweepOtherSessions(id);
      try {
        timers = validTimers(JSON.parse(localStorage.getItem(storageKey(id)) ?? '[]'));
      } catch {
        /* Invalid legacy storage is discarded. */
      }
      const legacy = $state.snapshot(timers);
      const href = url();
      void queue(
        id,
        (state) => ({ ...state, url: href, timers: state.initialized ? state.timers : legacy }),
        true
      );
    },
    start(stepIndex: number, seconds: number, label: string) {
      const id = sessionId();
      if (
        !id ||
        !Number.isInteger(stepIndex) ||
        stepIndex < 0 ||
        !Number.isFinite(seconds) ||
        seconds <= 0
      )
        return;
      if (loadedFor !== id) this.load();
      unlockAudio();
      void requestTimerNotificationPermission();
      now = Date.now();
      const timer = { stepIndex, endsAt: now + seconds * 1000, label };
      timers = [...timers.filter((t) => t.stepIndex !== stepIndex), timer];
      cache(id);
      const href = url();
      void queue(id, (state) => ({
        ...state,
        url: href,
        timers: [...state.timers.filter((t) => t.stepIndex !== stepIndex), timer]
      })).then(() => closeTimerNotification(id, stepIndex));
    },
    pause(stepIndex: number) {
      const id = loadedFor;
      const timer = timers.find((t) => t.stepIndex === stepIndex);
      now = Date.now();
      if (!id || !timer || timer.pausedRemaining !== undefined || timer.endsAt <= now) return;
      const paused = { ...timer, pausedRemaining: remaining(timer) };
      timers = timers.map((t) => (t === timer ? paused : t));
      cache(id);
      void queue(id, (state) => ({
        ...state,
        timers: state.timers.map((t) =>
          t.stepIndex === stepIndex && t.endsAt === timer.endsAt ? paused : t
        )
      })).then(() => closeTimerNotification(id, stepIndex));
    },
    resume(stepIndex: number) {
      const id = loadedFor;
      const timer = timers.find((t) => t.stepIndex === stepIndex);
      if (!id || !timer || timer.pausedRemaining === undefined) return;
      now = Date.now();
      unlockAudio();
      const resumed = { stepIndex, label: timer.label, endsAt: now + timer.pausedRemaining * 1000 };
      timers = timers.map((t) => (t === timer ? resumed : t));
      cache(id);
      void queue(id, (state) => ({
        ...state,
        timers: state.timers.map((t) =>
          t.stepIndex === stepIndex && t.endsAt === timer.endsAt && t.pausedRemaining !== undefined
            ? resumed
            : t
        )
      }));
    },
    dismiss(stepIndex: number) {
      const id = loadedFor;
      if (!id) return;
      timers = timers.filter((t) => t.stepIndex !== stepIndex);
      cache(id);
      void queue(id, (state) => ({
        ...state,
        timers: state.timers.filter((t) => t.stepIndex !== stepIndex)
      })).then(() => closeTimerNotification(id, stepIndex));
    },
    consumeNextStep() {
      const id = loadedFor;
      const index = nextStep;
      nextStep = undefined;
      if (id && index !== undefined)
        void queue(id, (state) => {
          if (state.nextStep === index) delete state.nextStep;
          return state;
        });
      return index;
    },
    refresh,
    tick(clock: Window = window): () => void {
      const read = () => {
        now = Date.now();
        checkAlarms();
      };
      const ticking = clock.setInterval(read, 1000);
      const onVisible = () => {
        if (clock.document.visibilityState === 'visible') {
          now = Date.now();
          void refresh().then(read);
        }
      };
      const onMessage = (event: MessageEvent) => {
        if (event.data?.type === 'culina:timers-changed' && event.data.sessionId === loadedFor)
          void refresh().then(read);
      };
      clock.document.addEventListener('visibilitychange', onVisible);
      navigator.serviceWorker?.addEventListener('message', onMessage);
      read();
      return () => {
        clock.clearInterval(ticking);
        clock.document.removeEventListener('visibilitychange', onVisible);
        navigator.serviceWorker?.removeEventListener('message', onMessage);
      };
    },
    /** Drops what is held for a session that ended (default: the loaded one). */
    clear(id: string | null = loadedFor) {
      if (id === loadedFor) {
        revision++;
        timers = [];
        nextStep = undefined;
        alerted.clear();
        loadedFor = null;
      }
      if (!id) return;
      try {
        localStorage.removeItem(storageKey(id));
      } catch {
        /* Optional. */
      }
      void queue(id, (state) => ({ ...state, timers: [], nextStep: undefined })).then(() =>
        closeTimerNotification(id)
      );
    }
  };
}
