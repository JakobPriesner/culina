import { PushNotifications } from '@capacitor/push-notifications';

/**
 * The installed app's side of import notifications (docs/native-app-design.md, 7.4): the store's device token, and opening
 * a tapped notification's path. The website never loads this (`build-tools/nativeOnly.ts`); it has Web Push.
 */

/** How long the store has to hand over a token before enabling gives up. */
const registrationDeadlineMs = 20_000;

/** Asks for permission (from the person's tap) and the store's device token; null when refused or not delivered. */
export async function requestDeviceToken(): Promise<string | null> {
  const permission = await PushNotifications.requestPermissions();

  if (permission.receive !== 'granted') {
    return null;
  }

  return new Promise((resolve) => {
    const done = (token: string | null) => {
      clearTimeout(timer);
      void registered.then((handle) => handle.remove());
      void failed.then((handle) => handle.remove());
      resolve(token);
    };
    const timer = setTimeout(() => done(null), registrationDeadlineMs);
    const registered = PushNotifications.addListener('registration', (token) => done(token.value));
    const failed = PushNotifications.addListener('registrationError', () => done(null));

    void PushNotifications.register().catch(() => done(null));
  });
}

/** Whether notifications are still allowed, so a restored registration is only claimed while they are. */
export async function notificationsAllowed(): Promise<boolean> {
  return (await PushNotifications.checkPermissions()).receive === 'granted';
}

/** Stops the store sending to this app, on turning notifications off or signing out. */
export async function stopDeviceToken(): Promise<void> {
  await PushNotifications.unregister().catch(() => {});
}

/** Opens what a tapped notification points at; only the app's own paths, never another address. */
export async function openTappedNotifications(open: (path: string) => void): Promise<void> {
  await PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
    const data = action.notification.data as { path?: unknown } | undefined;
    const path = data?.path;

    if (typeof path === 'string' && path.startsWith('/') && !path.startsWith('//')) {
      open(path);
    }
  });
}
