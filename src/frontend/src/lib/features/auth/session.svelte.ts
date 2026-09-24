import { forgetCachedResponses, http, request, type AppError } from '$api';
import { forgetEveryDraft } from '$features/recipes/editor/journal';
import { forgetCachedReads } from '$shell/connection.svelte';
import { readDevice, writeDevice } from '$shell/deviceStorage';
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
 * `unknown` is the state the app boots in, and the only one where the boot
 * skeleton is the right answer: there is genuinely nothing to show until we
 * know whether this is a signed-in person or a stranger.
 *
 * `unavailable` is the difference between "you are not signed in" and "we could
 * not ask". Only a 401 means the first. A timeout, a dropped connection or a
 * backend that is still starting up means the second, and treating it as the
 * first is what puts a sign-in form in front of somebody whose cookie is
 * perfectly valid — the single most common way an app looks like it forgets
 * who you are.
 */
export type SessionStatus = 'unknown' | 'authenticated' | 'anonymous' | 'unavailable';

/** Which household is being looked at. A preference, not private data. */
const activeHouseholdKey = 'culina.household';

/**
 * Which of the two boot skeletons this device should paint next time.
 *
 * Read by the inline script in `app.html`, before any of this code exists. The
 * document cannot know whether a session is live until the server answers, and
 * the last answer is right almost every time.
 */
const bootHintKey = 'culina.boot';

/**
 * How long boot waits to find out who is signed in.
 *
 * Shorter than the 15 seconds every other request gets, because this one is
 * different in kind: the app layout's guard awaits it before anything renders,
 * so until it answers the screen holds the static boot logo and nothing else.
 * Fifteen seconds of that is indistinguishable from a broken app, and it is
 * what a backend that hangs rather than refuses actually produced.
 *
 * Giving up costs nothing: the status becomes "unavailable" rather than
 * "signed out", which is the screen that says so and offers to try again, and
 * the cookie is still in the jar when they do.
 */
const bootDeadlineMs = 4_000;

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
    // Already answered. The guard in the app layout runs on every navigation —
    // including the ones a hover speculatively preloads — and asking the server
    // who is signed in again each time is two requests for an answer that has
    // not changed since boot. `refresh()` is how a caller says it has.
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
    writeDevice(activeHouseholdKey, householdId);
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
    writeDevice(bootHintKey, 'auth');
  }

  reset(): void {
    this.#status = 'unknown';
    this.#user = null;
    this.#activeHouseholdId = null;
  }

  async #load(signal?: AbortSignal): Promise<void> {
    // In parallel: both need the same cookie, and waiting for the first to
    // decide whether to ask for the second would cost a round trip on the one
    // request path that is always on the critical path.
    const [me, settings] = await Promise.all([
      request(() => http.GET('/api/v1/users/me', { signal })),
      request(() => http.GET('/api/v1/users/me/settings', { signal }))
    ]);

    if (!me.ok) {
      this.#user = null;
      // 401 is the server saying there is no session. Anything else is us
      // failing to ask, which is not the same answer and must not sign anyone
      // out; the app offers to try again instead.
      this.#status = me.error.status === 401 ? 'anonymous' : 'unavailable';

      if (this.#status === 'anonymous') {
        writeDevice(bootHintKey, 'auth');
      }

      return;
    }

    this.#user = me.value;
    this.#status = 'authenticated';
    writeDevice(bootHintKey, 'app');
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
    const remembered = readDevice(activeHouseholdKey);

    if (remembered && households.some((h) => h.householdId === remembered)) {
      return remembered;
    }

    return households[0]?.householdId ?? null;
  }
}

export const session = new SessionStore();

registerStore(() => session.reset());
