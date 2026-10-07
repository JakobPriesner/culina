import { forgetCachedResponses, http, request, type AppError } from '$api';
import { forgetEveryDraft } from '$features/recipes/editor/journal';
import { forgetEveryLastDraft } from '$features/recipes/editor/lastDraft';
import { forgetEveryRecentSearch } from '$features/recipes/search/recentSearches';
import { forgetCachedReads } from '$shell/connection.svelte';
import { readDevice, writeDevice } from '$shell/deviceStorage';
import { registerStore, resetAllStores } from '$shell/stores';
import { preferences } from '$shell/preferences.svelte';
import type { LocaleChoice } from '$shell/i18n';

import type { components } from '$api/generated/schema';

type CurrentUser = components['schemas']['UsersGetCurrentResponse'];
type Membership = components['schemas']['UsersGetCurrentHouseholdMembership'];

/**
 * `unavailable` means we could not ask (timeout, dropped connection, backend starting),
 * not "signed out"; only a 401 is the latter.
 */
export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous' | 'unavailable';

/** Remembered on the device; a preference, not private data. */
const activeHouseholdKey = 'culina.household';

/** Which boot skeleton to paint next time; read by the inline script in `app.html` before this code exists. */
const bootHintKey = 'culina.boot';

/**
 * Boot waits 4s, not the usual 15s: the layout guard blocks rendering on it.
 * Giving up yields "unavailable", not "signed out".
 */
const bootDeadlineMs = 4_000;

/** Drops per-account device data (drafts, recent searches) for everybody but `keep`. */
function forgetAccounts(keep?: string): void {
  forgetEveryDraft(keep);
  forgetEveryLastDraft(keep);
  forgetEveryRecentSearch(keep);
}

class SessionStore {
  #status = $state<SessionStatus>('unknown');
  #user = $state<CurrentUser | null>(null);
  #activeHouseholdId = $state<string | null>(null);

  #resolving: Promise<void> | null = null;

  get status(): SessionStatus {
    return this.#status;
  }

  get user(): CurrentUser | null {
    return this.#user;
  }

  get households(): readonly Membership[] {
    return this.#user?.households ?? [];
  }

  get activeHousehold(): Membership | null {
    return this.households.find((h) => h.householdId === this.#activeHouseholdId) ?? null;
  }

  get activeHouseholdId(): string | null {
    return this.activeHousehold?.householdId ?? null;
  }

  /** Inherited households by id with their names, for labelling a recipe card's origin. */
  get inheritedFrom(): Readonly<Record<string, string>> {
    return Object.fromEntries(
      (this.activeHousehold?.inheritsFrom ?? []).map((h) => [h.householdId, h.name])
    );
  }

  /** A household's name if the user is in it or the active one inherits from it, else null. */
  householdName(householdId: string): string | null {
    return (
      this.households.find((h) => h.householdId === householdId)?.name ??
      this.activeHousehold?.inheritsFrom.find((h) => h.householdId === householdId)?.name ??
      null
    );
  }

  /** Re-reads the session once the server knows something new (household created or joined). */
  refresh(): Promise<void> {
    return this.#load();
  }

  /** Resolves the session, at most once per boot. */
  resolve(): Promise<void> {
    // Skip when answered: the guard runs on every navigation, hover preloads included.
    if (this.#status === 'authenticated' || this.#status === 'anonymous') {
      return Promise.resolve();
    }

    this.#resolving ??= this.#load(AbortSignal.timeout(bootDeadlineMs)).finally(() => {
      this.#resolving = null;
    });

    return this.#resolving;
  }

  async signIn(email: string, password: string): Promise<AppError | null> {
    const result = await request(() =>
      http.POST('/api/v1/sessions', { body: { email, password } })
    );

    if (!result.ok) {
      return result.error;
    }

    // Drop the previous person's reads on the way in too: a closed browser never signed out.
    forgetCachedReads();

    // Re-read: the sign-in response lacks the households.
    await this.#load();

    // Keep only this person's unsent recipes (what an expired session left).
    if (this.#user) {
      forgetAccounts(this.#user.userId);
    }

    return null;
  }

  async signOut(): Promise<void> {
    const { importPush } = await import('$features/import/push.svelte');
    await importPush.disable().catch(() => {});
    await request(() => http.DELETE('/api/v1/sessions/current'));

    // Cleared even if the request failed, so the previous person's data never stays on screen.
    this.end();
    // Account-scoped keys hide drafts; only this deletes them.
    forgetAccounts();
  }

  selectHousehold(householdId: string): void {
    if (!this.households.some((h) => h.householdId === householdId)) {
      return;
    }

    this.#activeHouseholdId = householdId;
    writeDevice(activeHouseholdKey, householdId);
  }

  /**
   * Clears session, stores and cached responses, but keeps unsent drafts:
   * this also runs when a session expires mid-sentence.
   */
  end(): void {
    resetAllStores();
    forgetCachedResponses();
    forgetCachedReads();
    this.#status = 'anonymous';
    writeDevice(bootHintKey, 'auth');
  }

  reset(): void {
    this.#status = 'unknown';
    this.#user = null;
    this.#activeHouseholdId = null;
  }

  async #load(signal?: AbortSignal): Promise<void> {
    // Parallel: both need the same cookie and this is always on the critical path.
    const [me, settings] = await Promise.all([
      request(() => http.GET('/api/v1/users/me', { signal })),
      request(() => http.GET('/api/v1/users/me/settings', { signal }))
    ]);

    if (!me.ok) {
      this.#user = null;
      // Only a 401 means signed out; anything else is a failed ask and must not sign anyone out.
      this.#status = me.error.status === 401 ? 'anonymous' : 'unavailable';

      if (this.#status === 'anonymous') {
        writeDevice(bootHintKey, 'auth');
      }

      return;
    }

    // The offline copy outlives deploys and may lack `inheritsFrom`; such a household inherits nothing.
    this.#user = {
      ...me.value,
      households: me.value.households.map((h) => ({ ...h, inheritsFrom: h.inheritsFrom ?? [] }))
    };
    this.#status = 'authenticated';
    writeDevice(bootHintKey, 'app');
    this.#activeHouseholdId = this.#chooseHousehold(me.value.households);

    // Server wins: a long-offline device must not overwrite a newer choice made elsewhere.
    if (settings.ok) {
      preferences.adopt(
        {
          locale: settings.value.locale as LocaleChoice,
          theme: settings.value.theme,
          mode: settings.value.mode as 'light' | 'dark' | 'system',
          measurementSystem: settings.value.measurementSystem as 'metric' | 'imperial'
        },
        { signedIn: true }
      );
    }
  }

  /** The last-viewed household if still a member, else the first. */
  #chooseHousehold(households: readonly Membership[]): string | null {
    const remembered = readDevice(activeHouseholdKey);

    if (remembered && households.some((h) => h.householdId === remembered)) {
      return remembered;
    }

    return households[0]?.householdId ?? null;
  }
}

export const session = new SessionStore();

registerStore(() => session.reset());
