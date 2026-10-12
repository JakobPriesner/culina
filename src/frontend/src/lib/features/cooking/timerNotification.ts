import { native } from '$api';
import { m } from '$shell/i18n';
import { closeNativeTimer, requestNativeTimerPermission } from './nativeTimers';
import { alarmCadence, type TimerNotice } from './timerState';

/* In the app the operating system rings timers it was given ahead of time (nativeTimers.ts); these are the website's. */

export async function requestTimerNotificationPermission(): Promise<void> {
  if (native) return requestNativeTimerPermission();
  if (typeof window === 'undefined' || !('Notification' in window)) return;
  if (Notification.permission === 'default') {
    try {
      await Notification.requestPermission();
    } catch {
      /* Optional. */
    }
  }
}

export async function notifyTimerDone(label: string, notice: TimerNotice): Promise<void> {
  if (
    native ||
    typeof window === 'undefined' ||
    !('Notification' in window) ||
    Notification.permission !== 'granted'
  )
    return;
  const title = label || m['kitchen.timer']();
  const options = {
    body: m['kitchen.timerFinished'](),
    icon: '/icon-192.png',
    badge: '/icon-192.png',
    tag: `culina-timer-${notice.sessionId}-${notice.stepIndex}`,
    renotify: true,
    data: notice,
    vibrate: alarmCadence,
    silent: false,
    actions: [
      { action: 'minute-1', title: m['kitchen.minute1']() },
      { action: 'minute-2', title: m['kitchen.minute2']() },
      { action: 'dismiss', title: m['app.dismiss']() },
      { action: 'next', title: m['cooking.next']() }
    ]
  };
  // Platforms may cap actions (commonly two) or omit them altogether.
  const limit = (Notification as typeof Notification & { maxActions?: number }).maxActions;
  if (typeof limit === 'number') options.actions = options.actions.slice(0, limit);
  try {
    const reg = await navigator.serviceWorker?.getRegistration();
    if (reg) {
      try {
        await reg.showNotification(title, options);
      } catch {
        const fallback = { ...options, actions: [] };
        await reg.showNotification(title, fallback);
      }
      return;
    }
  } catch {
    /* Fall back to a window notification. */
  }
  try {
    const fallback = { ...options, actions: [] };
    const notification = new Notification(title, fallback);
    notification.onclick = () => {
      window.focus();
      window.location.assign(notice.url);
      notification.close();
    };
  } catch {
    /* Notifications must never break a timer. */
  }
}

export async function closeTimerNotification(sessionId: string, stepIndex?: number) {
  if (native) return closeNativeTimer(sessionId, stepIndex);
  try {
    const reg = await navigator.serviceWorker?.getRegistration();
    const notifications = await reg?.getNotifications();
    for (const notification of notifications ?? []) {
      const data = notification.data as Partial<TimerNotice> | undefined;
      if (
        data?.sessionId === sessionId &&
        (stepIndex === undefined || data.stepIndex === stepIndex)
      )
        notification.close();
    }
  } catch {
    /* Optional. */
  }
}
