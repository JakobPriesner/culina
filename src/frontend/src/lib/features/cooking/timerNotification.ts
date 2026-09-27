/**
 * Triggers a native system notification when a kitchen timer has elapsed.
 *
 * Uses the active Service Worker registration when available, which allows
 * notifications with actions on iOS/Android PWA standalone mode.
 */
export async function requestTimerNotificationPermission(): Promise<void> {
  if (
    typeof window !== 'undefined' &&
    'Notification' in window &&
    Notification.permission === 'default'
  ) {
    try {
      await Notification.requestPermission();
    } catch {
      // Ignored
    }
  }
}

export function notifyTimerDone(label: string): void {
  if (
    typeof window === 'undefined' ||
    !('Notification' in window) ||
    Notification.permission !== 'granted'
  ) {
    return;
  }

  const title = label || 'Timer';
  const options: NotificationOptions = {
    body: 'Timer finished!',
    icon: '/icon-192.png',
    badge: '/icon-192.png',
    tag: `culina-timer-${label}`
  };

  if ('serviceWorker' in navigator) {
    void navigator.serviceWorker.ready
      .then((reg) => {
        void reg.showNotification(title, options);
      })
      .catch(() => {
        try {
          new Notification(title, options);
        } catch {
          // Ignored
        }
      });
  } else {
    try {
      new Notification(title, options);
    } catch {
      // Ignored
    }
  }
}
