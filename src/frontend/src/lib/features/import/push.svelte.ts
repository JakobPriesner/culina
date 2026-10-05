import { http, request } from '$api';
import { preferences } from '$shell/preferences.svelte';

class ImportPush {
  enabled = $state(false);
  busy = $state(false);
  failed = $state(false);
  get supported(): boolean {
    return (
      typeof window !== 'undefined' &&
      'Notification' in window &&
      'serviceWorker' in navigator &&
      'PushManager' in window
    );
  }
  async restore(): Promise<void> {
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
}
export const importPush = new ImportPush();
