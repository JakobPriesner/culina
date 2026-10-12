import { LocalNotifications, type ActionPerformed } from '@capacitor/local-notifications';

import { goto } from '$app/navigation';
import { m } from '$shell/i18n';

import { applyTimerAction, type KitchenTimer, type TimerNotice } from './timerState';

/**
 * The installed app's kitchen timers, rung by the operating system (docs/native-app-design.md, 4.2): the page publishes
 * the whole set of timers whenever it changes and this schedules exactly that, so an alarm rings with the app asleep or
 * closed. Buttons on a notification go through the same `applyTimerAction` as the website's worker, so a stale button
 * (an `endsAt` that no longer matches) changes nothing. Loaded only by the app's bundle (build-tools/nativeOnly.ts).
 */

export interface NativeTimerSnapshot {
  readonly sessionId: string;
  /** The cooking route, opened by tapping a notification. */
  readonly url: string;
  readonly timers: readonly KitchenTimer[];
}

/** Sent to the page after a notification button changed the stored timers, as the worker's `culina:timers-changed`. */
export const timersChangedEvent = 'culina:timers-changed';

const actionType = 'culina-timer';
const channel = 'kitchen-alarms';
const timerActions = ['minute-1', 'minute-2', 'dismiss', 'next'] as const;

let ready: Promise<void> | null = null;
let syncing: Promise<void> = Promise.resolve();

/** Registers the buttons, the Android channel and the listener; once, from the native shell. */
export function startNativeTimers(): Promise<void> {
  ready ??= (async () => {
    await LocalNotifications.registerActionTypes({
      types: [
        {
          id: actionType,
          // Foreground: the change is applied by the page, which iOS does not wake for a background action.
          actions: [
            { id: 'minute-1', title: m['kitchen.minute1'](), foreground: true },
            { id: 'minute-2', title: m['kitchen.minute2'](), foreground: true },
            { id: 'dismiss', title: m['app.dismiss'](), foreground: true },
            { id: 'next', title: m['cooking.next'](), foreground: true }
          ]
        }
      ]
    });

    await LocalNotifications.createChannel({
      id: channel,
      name: m['kitchen.timer'](),
      description: m['kitchen.timerFinished'](),
      // High: a kitchen alarm makes a sound and shows over what is on screen.
      importance: 5,
      visibility: 1,
      vibration: true
    }).catch(() => {
      // iOS has no channels.
    });

    await LocalNotifications.addListener('localNotificationActionPerformed', (event) => {
      void act(event);
    });
  })().catch(() => {});

  return ready;
}

/** Asked the moment the first timer starts, as the website asks for notification permission. */
export async function requestNativeTimerPermission(): Promise<void> {
  try {
    const { display } = await LocalNotifications.checkPermissions();

    if (display === 'prompt' || display === 'prompt-with-rationale') {
      await LocalNotifications.requestPermissions();
    }
  } catch {
    // Timers still run in the app; only the alarm with the app asleep is lost.
  }
}

/**
 * Makes the scheduled alarms exactly the running timers of `snapshot` (none for null): unchanged ones are left, moved or
 * new ones scheduled, paused, dismissed and finished ones cancelled. Serialised, so a burst of changes settles in order.
 */
export function syncNativeTimers(snapshot: NativeTimerSnapshot | null): Promise<void> {
  syncing = syncing.then(() => reconcile(snapshot)).catch(() => {});

  return syncing;
}

/** Takes finished timers' notifications off the screen: one step's, or the whole session's. */
export async function closeNativeTimer(sessionId: string, stepIndex?: number): Promise<void> {
  try {
    const { notifications } = await LocalNotifications.getDeliveredNotifications();
    const ids = notifications
      .filter((delivered) => {
        const notice = noticeIn(delivered.extra);

        return (
          notice?.sessionId === sessionId &&
          (stepIndex === undefined || notice.stepIndex === stepIndex)
        );
      })
      .map((delivered) => delivered.id);

    if (ids.length > 0) {
      await LocalNotifications.removeDeliveredNotificationsById({ ids });
    }
  } catch {
    // Optional, as on the website.
  }
}

/** On sign-out: no alarm of the previous person's may ring for the next. */
export async function forgetNativeTimers(): Promise<void> {
  await syncNativeTimers(null);

  try {
    const { notifications } = await LocalNotifications.getDeliveredNotifications();
    const ids = notifications
      .filter((delivered) => noticeIn(delivered.extra))
      .map((delivered) => delivered.id);

    if (ids.length > 0) {
      await LocalNotifications.removeDeliveredNotificationsById({ ids });
    }
  } catch {
    // Nothing delivered, nothing to take away.
  }
}

async function reconcile(snapshot: NativeTimerSnapshot | null): Promise<void> {
  await startNativeTimers();

  const now = Date.now();
  const wanted = (snapshot?.timers ?? [])
    .filter((timer) => timer.pausedRemaining === undefined && timer.endsAt > now)
    .map((timer): TimerNotice => ({
      type: 'culina:timer',
      sessionId: snapshot!.sessionId,
      stepIndex: timer.stepIndex,
      endsAt: timer.endsAt,
      url: snapshot!.url
    }));
  const labels = new Map(snapshot?.timers.map((timer) => [timer.stepIndex, timer.label]) ?? []);

  const { notifications } = await LocalNotifications.getPending();
  const scheduled = notifications.flatMap((pending) => {
    const notice = noticeIn(pending.extra);

    return notice ? [{ id: pending.id, notice }] : [];
  });

  const stale = scheduled.filter(({ notice }) => !wanted.some((want) => same(want, notice)));
  const missing = wanted.filter((want) => !scheduled.some(({ notice }) => same(want, notice)));

  if (stale.length > 0) {
    await LocalNotifications.cancel({ notifications: stale.map(({ id }) => ({ id })) });
  }

  if (missing.length > 0) {
    await LocalNotifications.schedule({
      notifications: missing.map((notice) => ({
        id: notificationId(notice),
        title: labels.get(notice.stepIndex) || m['kitchen.timer'](),
        body: m['kitchen.timerFinished'](),
        schedule: { at: new Date(notice.endsAt), allowWhileIdle: true },
        actionTypeId: actionType,
        channelId: channel,
        interruptionLevel: 'timeSensitive',
        extra: notice
      }))
    });
  }
}

async function act({ actionId, notification }: ActionPerformed): Promise<void> {
  const notice = noticeIn(notification.extra);

  if (!notice) {
    return;
  }

  const action = timerActions.find((known) => known === actionId);

  if (action && (await applyTimerAction(notice, action))) {
    window.dispatchEvent(
      new CustomEvent(timersChangedEvent, { detail: { sessionId: notice.sessionId } })
    );
  }

  // The notification itself, or "Next step": back to the recipe being cooked.
  if (actionId === 'tap' || action === 'next') {
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- a path this app built (`kitchenTimers`' url).
    await goto(notice.url);
  }
}

/** One notification per timer: the same step of the same session always replaces its own. */
function notificationId({ sessionId, stepIndex }: TimerNotice): number {
  let hash = 0;

  for (const character of `${sessionId}:${stepIndex}`) {
    hash = (Math.imul(hash, 31) + character.charCodeAt(0)) | 0;
  }

  // Positive and within Java's int, which Android's notification ids are.
  return hash & 0x7fffffff;
}

const same = (a: TimerNotice, b: TimerNotice) =>
  a.sessionId === b.sessionId && a.stepIndex === b.stepIndex && a.endsAt === b.endsAt;

function noticeIn(extra: unknown): TimerNotice | null {
  if (typeof extra !== 'object' || extra === null) {
    return null;
  }

  const notice = extra as Partial<TimerNotice>;

  return notice.type === 'culina:timer' &&
    typeof notice.sessionId === 'string' &&
    typeof notice.stepIndex === 'number' &&
    typeof notice.endsAt === 'number' &&
    typeof notice.url === 'string'
    ? (notice as TimerNotice)
    : null;
}
