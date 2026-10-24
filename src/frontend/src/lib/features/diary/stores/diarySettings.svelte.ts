import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import type { DiarySettings } from '../types';

export interface DiarySettingsChange {
  readonly enabled: boolean;
  readonly kcalTarget: number | null;
  readonly proteinTarget: number | null;
  readonly headline: 'eaten' | 'remaining';
  /** When switching off: delete every entry too. */
  readonly eraseEntries?: boolean;
}

/**
 * Whether this person keeps a food diary, and their own targets. Read once per session: every diary surface (its
 * place in the navigation, the line under the Today card, logging a portion after "made it") asks `enabled` and
 * shows nothing at all while it is off.
 */
class DiarySettingsStore {
  #settings = $state<DiarySettings | null>(null);

  get settings(): DiarySettings | null {
    return this.#settings;
  }

  get enabled(): boolean {
    return this.#settings?.enabled ?? false;
  }

  async load(): Promise<void> {
    const result = await request(() => http.GET('/api/v1/users/me/settings/diary'));

    if (result.ok) {
      this.#settings = result.value;
    }
  }

  /** Switching on agrees to the wording the settings carry (`currentConsentVersion`), which the page shows beside the switch. */
  async save(change: DiarySettingsChange): Promise<AppError | null> {
    const switchingOn = change.enabled && !this.enabled;
    const result = await request(() =>
      http.PUT('/api/v1/users/me/settings/diary', {
        body: {
          enabled: change.enabled,
          consentVersion: switchingOn ? (this.#settings?.currentConsentVersion ?? null) : null,
          kcalTarget: change.kcalTarget,
          proteinTarget: change.proteinTarget,
          headline: change.headline,
          eraseEntries: change.eraseEntries ?? null
        }
      })
    );

    if (!result.ok) {
      return result.error;
    }

    this.#settings = result.value;

    return null;
  }

  reset(): void {
    this.#settings = null;
  }
}

export const diarySettings = new DiarySettingsStore();

registerStore(() => diarySettings.reset());
