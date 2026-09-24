import { http, request } from '$api';

import { readDevice, writeDevice } from './deviceStorage';
import { applyLocale, detectLocale, isLocale } from './i18n';
import {
  defaultAppearance,
  parseAppearance,
  storageKey,
  type Mode,
  type ResolvedMode
} from './appearance';

/**
 * How this person wants the app to look and read.
 *
 * The server is the source of truth, so the same account looks the same on a
 * phone and a laptop. `localStorage` is the cache that prevents the flash: it
 * is what the inline script in `app.html` reads before the first paint, and it
 * is written first so a change survives a reload even when the request fails.
 */
export type MeasurementSystem = 'metric' | 'imperial';

interface Preferences {
  locale: string;
  theme: string;
  mode: Mode;
  measurementSystem: MeasurementSystem;
}

const initial: Preferences = {
  locale: 'en',
  ...defaultAppearance,
  measurementSystem: 'metric'
};

class PreferencesStore {
  #values = $state<Preferences>({ ...initial });

  /** What the device asks for, watched so `system` follows it live. */
  #deviceMode = $state<ResolvedMode>('light');

  /** True when a change could not be sent and is waiting for the network. */
  #unsynced = $state(false);

  #signedIn = false;

  get theme(): string {
    return this.#values.theme;
  }

  get mode(): Mode {
    return this.#values.mode;
  }

  get locale(): string {
    return this.#values.locale;
  }

  get measurementSystem(): MeasurementSystem {
    return this.#values.measurementSystem;
  }

  /** What the document is actually painted in. */
  get resolvedMode(): ResolvedMode {
    return this.#values.mode === 'system' ? this.#deviceMode : this.#values.mode;
  }

  get unsynced(): boolean {
    return this.#unsynced;
  }

  /**
   * Picks up what the inline script already applied and starts following the
   * device. Called once by the app shell.
   */
  start(): () => void {
    this.#values = {
      ...this.#values,
      ...parseAppearance(readDevice(storageKey)),
      locale: detectLocale()
    };

    const query = globalThis.matchMedia?.('(prefers-color-scheme: dark)');

    if (!query) {
      return () => {};
    }

    const follow = () => {
      this.#deviceMode = query.matches ? 'dark' : 'light';
      this.#paint();
    };

    follow();
    query.addEventListener('change', follow);
    globalThis.addEventListener?.('online', this.#retry);

    return () => {
      query.removeEventListener('change', follow);
      globalThis.removeEventListener?.('online', this.#retry);
    };
  }

  /**
   * Replaces everything with what the server has.
   *
   * Called on boot and after signing in. The server wins because a device that
   * has been offline for a week should not push a week-old choice over a newer
   * one made elsewhere.
   */
  adopt(values: Partial<Preferences>, options: { signedIn: boolean }): void {
    this.#signedIn = options.signedIn;

    if (values.locale && isLocale(values.locale)) {
      applyLocale(values.locale);
    }

    this.#values = { ...this.#values, ...values };
    this.#cache();
    this.#paint();
  }

  setTheme(theme: string): void {
    this.#change({ theme });
  }

  setMode(mode: Mode): void {
    this.#change({ mode });
  }

  setLocale(locale: string): void {
    if (!isLocale(locale)) {
      return;
    }

    applyLocale(locale);
    this.#change({ locale });
  }

  setMeasurementSystem(measurementSystem: MeasurementSystem): void {
    this.#change({ measurementSystem });
  }

  /** Back to a signed-out default. Called on sign-out, and by tests. */
  reset(): void {
    this.#signedIn = false;
    this.#unsynced = false;
    this.#values = { ...initial };
    this.#paint();
  }

  /**
   * Local first, then the server.
   *
   * The order is the point: the change is visible and durable before the
   * network is involved, so toggling to dark mode on a train works and is still
   * dark after a reload.
   */
  #change(patch: Partial<Preferences>): void {
    this.#values = { ...this.#values, ...patch };
    this.#cache();
    this.#paint();
    void this.#push();
  }

  #paint(): void {
    const root = globalThis.document?.documentElement;

    if (root) {
      root.dataset['theme'] = this.#values.theme;
      root.dataset['mode'] = this.resolvedMode;
      root.lang = this.#values.locale;
    }
  }

  #cache(): void {
    writeDevice(storageKey, JSON.stringify({ theme: this.#values.theme, mode: this.#values.mode }));
  }

  async #push(): Promise<void> {
    if (!this.#signedIn) {
      return;
    }

    const result = await request(() =>
      http.PUT('/api/v1/users/me/settings', { body: { ...this.#values } })
    );

    // A failure is not worth interrupting anyone for: the choice is already
    // applied and stored. It syncs on the next change or when the device comes
    // back online.
    this.#unsynced = !result.ok;
  }

  #retry = () => {
    if (this.#unsynced) {
      void this.#push();
    }
  };
}

export const preferences = new PreferencesStore();
