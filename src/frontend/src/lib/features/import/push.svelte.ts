import { http, native, registerAtRelay, relayUrl, request, revokeAtRelay } from '$api';
import { preferences } from '$shell/preferences.svelte';

/** This install's id, kept across sign-ins so the same phone replaces its row on the server rather than adding one. */
const deviceIdKey = 'culina.push.deviceId';

/** The relay token this install registered, while notifications are on. */
const relayTokenKey = 'culina.push.relayToken';

/** Loaded only by the app's bundle; the website's build replaces it with a stub (build-tools/nativeOnly.ts). */
const nativePush = () => import('./nativePush');

class ImportPush {
  enabled = $state(false);
  busy = $state(false);
  failed = $state(false);

  /** Whether this app's server names a push relay; asked once per launch, false until it answers. */
  #relayOn = $state(false);

  get supported(): boolean {
    if (native) {
      // The app is reached through its publisher's relay, and only when the server names one (docs/native-app-design.md, 7.3).
      return relayUrl !== '' && this.#relayOn;
    }

    return (
      typeof window !== 'undefined' &&
      'Notification' in window &&
      'serviceWorker' in navigator &&
      'PushManager' in window
    );
  }

  async restore(): Promise<void> {
    if (native) {
      await this.#restoreApp();

      return;
    }

    if (!this.supported || Notification.permission !== 'granted') {
      this.enabled = false;
      return;
    }
    try {
      const registration = await navigator.serviceWorker.getRegistration();
      const subscription = await registration?.pushManager.getSubscription();
      this.enabled = subscription ? await this.register(subscription) : false;
    } catch {
      this.enabled = false;
    }
  }

  async enable(): Promise<void> {
    if (!this.supported || this.busy) return;
    this.busy = true;
    this.failed = false;
    try {
      if (native) {
        this.enabled = await this.#enableApp();
        this.failed = !this.enabled;
        return;
      }
      // Permission must be requested directly from this button gesture.
      if ((await Notification.requestPermission()) !== 'granted') {
        this.failed = true;
        return;
      }
      const registration = await navigator.serviceWorker.getRegistration();
      if (!registration?.active) {
        this.failed = true;
        return;
      }
      const key = await request(() => http.GET('/api/v1/push/key'));
      if (!key.ok) {
        this.failed = true;
        return;
      }
      const raw = atob(key.value.publicKey.replace(/-/g, '+').replace(/_/g, '/'));
      const bytes = Uint8Array.from(raw, (ch) => ch.charCodeAt(0));
      const subscription =
        (await registration.pushManager.getSubscription()) ??
        (await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: bytes
        }));
      this.enabled = await this.register(subscription);
      this.failed = !this.enabled;
    } catch {
      this.failed = true;
    } finally {
      this.busy = false;
    }
  }

  async disable(): Promise<void> {
    if (native) {
      await this.#disableApp();
      return;
    }
    if (!this.supported) return;
    const registration = await navigator.serviceWorker.getRegistration();
    const subscription = await registration?.pushManager.getSubscription();
    if (subscription) {
      const result = await request(() =>
        http.DELETE('/api/v1/push/subscription', {
          params: { query: { endpoint: subscription.endpoint } }
        })
      );
      if (!result.ok) {
        this.failed = true;
      }
      await subscription.unsubscribe();
    }
    this.enabled = false;
  }

  private async register(subscription: PushSubscription): Promise<boolean> {
    const json = subscription.toJSON();
    const result = await request(() =>
      http.PUT('/api/v1/push/subscription', {
        body: {
          endpoint: subscription.endpoint,
          p256dh: json.keys?.p256dh ?? '',
          auth: json.keys?.auth ?? '',
          language: preferences.locale
        }
      })
    );
    return result.ok;
  }

  /** On launch: whether the server names a relay, and whether this install is still registered with both. */
  async #restoreApp(): Promise<void> {
    if (relayUrl === '') {
      this.enabled = false;
      return;
    }

    const relay = await request(() => http.GET('/api/v1/push/relay'));
    this.#relayOn = relay.ok && relay.value.available;

    const relayToken = read(relayTokenKey);

    // Re-registered each launch: it renews the language and finds out at once if the server forgot the device.
    this.enabled =
      this.#relayOn &&
      relayToken !== null &&
      (await (await nativePush()).notificationsAllowed()) &&
      (await this.#registerDevice(relayToken));
  }

  /** The store's token, the relay's token for it, and the server told: in that order, each from the person's tap. */
  async #enableApp(): Promise<boolean> {
    const deviceToken = await (await nativePush()).requestDeviceToken();

    if (!deviceToken) {
      return false;
    }

    const relayToken = await registerAtRelay(deviceToken);

    if (!relayToken) {
      return false;
    }

    write(relayTokenKey, relayToken);

    return this.#registerDevice(relayToken);
  }

  /** Forgets this install everywhere it was registered, on turning notifications off and on signing out. */
  async #disableApp(): Promise<void> {
    const relayToken = read(relayTokenKey);
    const deviceId = read(deviceIdKey);

    if (deviceId) {
      const result = await request(() =>
        http.DELETE('/api/v1/push/devices/{deviceId}', { params: { path: { deviceId } } })
      );
      if (!result.ok) {
        this.failed = true;
      }
    }

    if (relayToken) {
      await revokeAtRelay(relayToken);
      remove(relayTokenKey);
    }

    await (await nativePush()).stopDeviceToken();
    this.enabled = false;
  }

  async #registerDevice(relayToken: string): Promise<boolean> {
    const deviceId = read(deviceIdKey) ?? write(deviceIdKey, crypto.randomUUID());
    const result = await request(() =>
      http.PUT('/api/v1/push/devices/{deviceId}', {
        params: { path: { deviceId } },
        body: { relayToken, language: preferences.locale }
      })
    );

    return result.ok;
  }
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string): string {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Not kept: the next launch registers again.
  }

  return value;
}

function remove(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Nothing kept to remove.
  }
}

export const importPush = new ImportPush();
