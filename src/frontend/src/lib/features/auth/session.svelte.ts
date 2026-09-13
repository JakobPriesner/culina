import { forgetCachedResponses, http, request, type AppError } from '$api';
import { forgetEveryDraft } from '$features/recipes/editor/journal';
import { forgetCachedReads } from '$shell/connection.svelte';
import { registerStore, resetAllStores } from '$shell/stores';
import { preferences } from '$shell/preferences.svelte';

import type { components } from '$api/generated/schema';

/**
 * Who is signed in, and where they can cook.
 *
 * Resolved once on boot from `GET /users/me`, which also returns the household
 * memberships — so the app knows everything it needs to render a shell after a
 * single round trip rather than two.
 */
type CurrentUser = components['schemas']['UsersGetCurrentResponse'];
type Membership = components['schemas']['UsersGetCurrentHouseholdMembership'];

/**
 * `unknown` is the state the app boots in, and the only one where a full-page
 * spinner is the right answer: there is genuinely nothing to show until we know
 * whether this is a signed-in person or a stranger.
 */
export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous';

/** Which household is being looked at. A preference, not private data. */
const activeHouseholdKey = 'culina.household';

class SessionStore {
  #status = $state<SessionStatus>('unknown');
  #user = $state<CurrentUser | null>(null);
  #activeHouseholdId = $state<string | null>(null);

  /** Shared by concurrent callers, so a boot never asks twice. */
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

  /**
   * Reads the session again.
   *
   * For the cases where the server now knows something the store does not — a
   * household just created or joined — rather than patching the store with
   * values invented on the client.
   */
  refresh(): Promise<void> {
    return this.#load();
  }

  /** Resolves the session, at most once per boot. */
  resolve(): Promise<void> {
    this.#resolving ??= this.#load().finally(() => {
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

    // Whatever this device read for the last person is not this person's to
    // see. On the way in as well as on the way out, because a browser closed
    // without signing out never reached the way out.
    forgetCachedReads();

    // A fresh read rather than trusting the sign-in response: it carries who
    // signed in, but not the households, and the shell needs both.
    await this.#load();

    return null;
  }

  async signOut(): Promise<void> {
    await request(() => http.DELETE('/api/v1/sessions/current'));

    // Cleared whatever the server said. A failed sign-out that leaves the
    // previous person's data on screen is worse than one that ends the session
    // locally and lets the cookie expire.
    this.end();
  }

  /** Chooses which household the app is looking at. */
  selectHousehold(householdId: string): void {
    if (!this.households.some((h) => h.householdId === householdId)) {
      return;
    }

    this.#activeHouseholdId = householdId;
    remember(activeHouseholdKey, householdId);
  }

  /** Everything goes: the session, every store, and the cached responses. */
  end(): void {
    resetAllStores();
    forgetCachedResponses();
    forgetCachedReads();
    // An unsent recipe belongs to whoever wrote it. Scoping the key by account
    // stops it being shown to the next person; only this stops it being kept.
    forgetEveryDraft();
    this.#status = 'anonymous';
  }

  reset(): void {
    this.#status = 'unknown';
    this.#user = null;
    this.#activeHouseholdId = null;
  }

  async #load(): Promise<void> {
    // In parallel: both need the same cookie, and waiting for the first to
    // decide whether to ask for the second would cost a round trip on the one
    // request path that is always on the critical path.
    const [me, settings] = await Promise.all([
      request(() => http.GET('/api/v1/users/me')),
      request(() => http.GET('/api/v1/users/me/settings'))
    ]);

    if (!me.ok) {
      this.#user = null;
      this.#status = 'anonymous';

      return;
    }

    this.#user = me.value;
    this.#status = 'authenticated';
    this.#activeHouseholdId = this.#chooseHousehold(me.value.households);

    // The server is the source of truth: a device that has been offline for a
    // week should not push a week-old choice over a newer one made elsewhere.
    if (settings.ok) {
      preferences.adopt(
        {
          locale: settings.value.locale,
          theme: settings.value.theme,
          mode: settings.value.mode as 'light' | 'dark' | 'system',
          measurementSystem: settings.value.measurementSystem as 'metric' | 'imperial'
        },
        { signedIn: true }
      );
    }
  }

  /**
   * The one they were last looking at, if they are still in it. Otherwise the
   * first, because a household picker on boot is a question nobody wants.
   */
  #chooseHousehold(households: readonly Membership[]): string | null {
    const remembered = recall(activeHouseholdKey);

    if (remembered && households.some((h) => h.householdId === remembered)) {
      return remembered;
    }

    return households[0]?.householdId ?? null;
  }
}

export const session = new SessionStore();

registerStore(() => session.reset());

/** Storage throws in a private window and where site data is blocked. */
function recall(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function remember(key: string, value: string): void {
  try {
    globalThis.localStorage?.setItem(key, value);
  } catch {
    // The choice still applies for this session; it just will not be recalled.
  }
}
