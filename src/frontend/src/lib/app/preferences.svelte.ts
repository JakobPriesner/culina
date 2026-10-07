import { http, request } from '$api';

import { readDevice, writeDevice } from './deviceStorage';
import {
  applyLocale,
  deviceLocale,
  isLocale,
  isLocaleChoice,
  rememberedLocale,
  type Locale,
  type LocaleChoice
} from './i18n';
import {
  defaultAppearance,
  parseAppearance,
  storageKey,
  type Mode,
  type ResolvedMode
} from './appearance';

/** Appearance and language: the server is the source of truth; `localStorage` is written first and read by the inline script in `app.html` to avoid a flash. */
export type MeasurementSystem = 'metric' | 'imperial';

interface Preferences {
  locale: LocaleChoice;
  theme: string;
  mode: Mode;
  measurementSystem: MeasurementSystem;
}

const initial: Preferences = {
  locale: 'system',
  ...defaultAppearance,
  measurementSystem: 'metric'
};

class PreferencesStore {
  #values = $state<Preferences>({ ...initial });

  #deviceMode = $state<ResolvedMode>('light');

  #deviceLocale = $state<Locale>(deviceLocale());

  #unsynced = $state(false);

  #signedIn = false;

  get theme(): string {
    return this.#values.theme;
  }

  get mode(): Mode {
    return this.#values.mode;
  }

  get locale(): Locale {
    const chosen = this.#values.locale;

    return isLocale(chosen) ? chosen : this.#deviceLocale;
  }

  get localeChoice(): LocaleChoice {
    return this.#values.locale;
  }

  get measurementSystem(): MeasurementSystem {
    return this.#values.measurementSystem;
  }

  get resolvedMode(): ResolvedMode {
    return this.#values.mode === 'system' ? this.#deviceMode : this.#values.mode;
  }

  get unsynced(): boolean {
    return this.#unsynced;
  }

  /** Picks up what the inline script applied and follows the device; called once by the app shell. */
  start(): () => void {
    this.#values = {
      ...this.#values,
      ...parseAppearance(readDevice(storageKey)),
      locale: rememberedLocale()
    };

    const followLanguage = () => {
      this.#deviceLocale = deviceLocale();
      this.#paint();
    };

    followLanguage();
    globalThis.addEventListener?.('languagechange', followLanguage);

    const query = globalThis.matchMedia?.('(prefers-color-scheme: dark)');

    if (!query) {
      return () => globalThis.removeEventListener?.('languagechange', followLanguage);
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
      globalThis.removeEventListener?.('languagechange', followLanguage);
      globalThis.removeEventListener?.('online', this.#retry);
    };
  }

  /** Replaces everything with the server's values (boot, sign-in); the server wins so a stale offline device cannot override a newer choice. */
  adopt(values: Partial<Preferences>, options: { signedIn: boolean }): void {
    this.#signedIn = options.signedIn;
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
    if (isLocaleChoice(locale)) {
      this.#change({ locale });
    }
  }

  setMeasurementSystem(measurementSystem: MeasurementSystem): void {
    this.#change({ measurementSystem });
  }

  reset(): void {
    this.#signedIn = false;
    this.#unsynced = false;
    this.#values = { ...initial };
    this.#deviceLocale = deviceLocale();
    this.#paint();
  }

  /** Local first, then the server, so a change is visible and durable before the network is involved. */
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
      root.lang = this.locale;
    }
  }

  /** The language too: it is what Paraglide reads, so it is written before anything re-renders. */
  #cache(): void {
    writeDevice(storageKey, JSON.stringify({ theme: this.#values.theme, mode: this.#values.mode }));
    applyLocale(this.#values.locale);
  }

  async #push(): Promise<void> {
    if (!this.#signedIn) {
      return;
    }

    const result = await request(() =>
      http.PUT('/api/v1/users/me/settings', { body: { ...this.#values } })
    );

    // Not worth interrupting anyone: the choice is applied and stored, and syncs on the next change or when back online.
    this.#unsynced = !result.ok;
  }

  #retry = () => {
    if (this.#unsynced) {
      void this.#push();
    }
  };
}

export const preferences = new PreferencesStore();
