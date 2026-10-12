import { registerPlugin, type PluginListenerHandle } from '@capacitor/core';

import { goto } from '$app/navigation';
import { m } from '$shell/i18n';

import { applyTimerAction, type KitchenTimer, type TimerNotice } from './timerState';

/**
 * The installed app's kitchen timers, kept by the operating system (docs/native-app-design.md, 4.2). Declarative down:
 * the page hands over the whole set of timers whenever it changes, and the app's own `CulinaTimers` plugin
 * (native/ios/App/App/CulinaTimers, native/android/.../timers) rings and shows exactly that: AlarmKit or notifications
 * and a Live Activity on iOS, alarms and an ongoing countdown on Android. Journal up: a button pressed on the lock
 * screen is applied there at once and recorded; the page drains the record and applies each through the website's
 * `applyTimerAction`, at the moment it was pressed, so both sides land on the same deadline and a stale button
 * changes nothing. Loaded only by the app's bundle (build-tools/nativeOnly.ts).
 */

export interface NativeTimerSnapshot {
  readonly sessionId: string;
  /** The cooking route, opened by tapping a notification or the Live Activity. */
  readonly url: string;
  readonly timers: readonly KitchenTimer[];
}

/** What the system shows, in the app's language: the native side has no message catalogue of its own. */
interface TimerText {
  readonly timer: string;
  readonly finished: string;
  readonly minute1: string;
  readonly minute2: string;
  readonly dismiss: string;
  readonly next: string;
  readonly paused: string;
  /** With `{count}` for the number of further timers. */
  readonly more: string;
}

/** One button press, as the native side recorded it. */
export interface NativeTimerAction {
  readonly sessionId: string;
  readonly stepIndex: number;
  /** The deadline the button was shown for. */
  readonly endsAt: number;
  readonly url: string;
  readonly action: 'minute-1' | 'minute-2' | 'dismiss' | 'next' | 'open';
  /** When it was pressed, epoch ms. */
  readonly at: number;
}

export interface CulinaTimersPlugin {
  /** Makes what the system rings and shows exactly these timers; a null session ends everything. */
  sync(options: {
    sessionId: string | null;
    url: string;
    timers: readonly KitchenTimer[];
    text: TimerText;
  }): Promise<void>;
  /** Hands over and forgets every recorded button press. */
  drain(): Promise<{ actions: NativeTimerAction[] }>;
  /** Takes finished timers' notifications off the screen: one step's, or the whole session's. */
  close(options: { sessionId: string; stepIndex?: number }): Promise<void>;
  requestPermissions(): Promise<{ display: string }>;
  /** Fired when a press was recorded while the page is alive. */
  addListener(event: 'action', listener: () => void): Promise<PluginListenerHandle>;
}

const CulinaTimers = registerPlugin<CulinaTimersPlugin>('CulinaTimers');

/** Sent to the page after recorded presses changed the stored timers, as the worker's `culina:timers-changed`. */
export const timersChangedEvent = 'culina:timers-changed';

const timerActions = ['minute-1', 'minute-2', 'dismiss', 'next'] as const;

let started: Promise<void> | null = null;
let syncing: Promise<void> = Promise.resolve();
let draining: Promise<void> = Promise.resolve();

/** Listens for presses; once, from the native shell, which also drains on every return to the app. */
export function startNativeTimers(): Promise<void> {
  started ??= (async () => {
    await CulinaTimers.addListener('action', () => void drainNativeTimers());
    await drainNativeTimers();
  })().catch(() => {});

  return started;
}

/** Applies every press made while the page was asleep or closed; serialised, so none is applied twice. */
export function drainNativeTimers(): Promise<void> {
  draining = draining.then(drain).catch(() => {});

  return draining;
}

/** Asked the moment the first timer starts, as the website asks for notification permission. */
export async function requestNativeTimerPermission(): Promise<void> {
  await CulinaTimers.requestPermissions().catch(() => {});
}

/** Hands the system the session's timers (none for null). Serialised, so a burst of changes settles in order. */
export function syncNativeTimers(snapshot: NativeTimerSnapshot | null): Promise<void> {
  syncing = syncing
    .then(() =>
      CulinaTimers.sync({
        sessionId: snapshot?.sessionId ?? null,
        url: snapshot?.url ?? '/',
        timers: snapshot?.timers ?? [],
        text: text()
      })
    )
    .catch(() => {});

  return syncing;
}

export async function closeNativeTimer(sessionId: string, stepIndex?: number): Promise<void> {
  await CulinaTimers.close(
    stepIndex === undefined ? { sessionId } : { sessionId, stepIndex }
  ).catch(() => {});
}

/** On sign-out: no alarm of the previous person's may ring for the next, and their presses are not applied. */
export async function forgetNativeTimers(): Promise<void> {
  await syncNativeTimers(null);
  await CulinaTimers.drain().catch(() => {});
}

async function drain(): Promise<void> {
  const { actions } = await CulinaTimers.drain();
  const changed = new Set<string>();
  let open: string | null = null;

  for (const press of actions) {
    const notice: TimerNotice = {
      type: 'culina:timer',
      sessionId: press.sessionId,
      stepIndex: press.stepIndex,
      endsAt: press.endsAt,
      url: press.url
    };
    const action = timerActions.find((known) => known === press.action);

    if (action && (await applyTimerAction(notice, action, press.at))) {
      changed.add(press.sessionId);
    }

    // The notification or activity itself, or "Next step": back to the recipe being cooked.
    if (press.action === 'open' || press.action === 'next') {
      open = press.url;
    }
  }

  for (const sessionId of changed) {
    window.dispatchEvent(new CustomEvent(timersChangedEvent, { detail: { sessionId } }));
  }

  if (open) {
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- a path this app built (`kitchenTimers`' url).
    await goto(open);
  }
}

function text(): TimerText {
  return {
    timer: m['kitchen.timer'](),
    finished: m['kitchen.timerFinished'](),
    minute1: m['kitchen.minute1'](),
    minute2: m['kitchen.minute2'](),
    dismiss: m['app.dismiss'](),
    next: m['cooking.next'](),
    paused: m['kitchen.native.paused'](),
    more: m['kitchen.native.more']({ count: '{count}' })
  };
}
