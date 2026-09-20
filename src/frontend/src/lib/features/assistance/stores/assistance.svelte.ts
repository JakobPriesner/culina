import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import {
  toAssistance,
  toProviderModels,
  type Assistance,
  type ProviderModels,
  type Usage
} from '../types';

import type { components } from '$api/generated/schema';

type UsageWire = components['schemas']['SettingsGetAssistanceUsageResponse'];

/**
 * How the assistant is set up, and what it has cost.
 *
 * Administrator-only, and read on demand rather than at boot: one person on the
 * instance can open this screen, and everybody else would be paying for the
 * request.
 *
 * The API key is deliberately not part of the state. No endpoint returns it, so
 * there is nothing to hold — what the form sends is a *new* key or nothing at
 * all, and "nothing at all" is what saving an unrelated change looks like.
 */
class AssistanceStore {
  #settings = $state<Assistance | null>(null);
  #usage = $state<Usage | null>(null);
  #models = $state<ProviderModels[]>([]);
  #loading = $state(false);
  #listing = $state(false);
  #saving = $state(false);
  #error = $state<AppError | null>(null);

  get settings(): Assistance | null {
    return this.#settings;
  }

  get usage(): Usage | null {
    return this.#usage;
  }

  /**
   * What each connected provider offers.
   *
   * Empty until it has been asked, and empty for a provider that did not
   * answer — the picker then falls back to a text box, so an unreachable
   * provider costs the convenience rather than the ability to configure it.
   */
  get models(): ProviderModels[] {
    return this.#models;
  }

  get loading(): boolean {
    return this.#loading;
  }

  /** Whether the providers are still being asked what they offer. */
  get listing(): boolean {
    return this.#listing;
  }

  get saving(): boolean {
    return this.#saving;
  }

  get error(): AppError | null {
    return this.#error;
  }

  /** Reads both the configuration and this month's spend. */
  async load(): Promise<void> {
    this.#loading = true;
    this.#error = null;

    // Deliberately not awaited with the other two. The form and the spend are
    // database reads; the model lists are one connection per provider to a
    // company somewhere else, made one after another. Waiting for all three
    // together held the entire screen blank for as long as the slowest of
    // those answers took — so the lists arrive on their own, and the pickers
    // they fill are the only thing that waits for them.
    void this.refreshModels();

    const [settings, usage] = await Promise.all([
      request(() => http.GET('/api/v1/settings/assistance')),
      request(() => http.GET('/api/v1/settings/assistance/usage'))
    ]);

    if (settings.ok) {
      this.#settings = toAssistance(settings.value);
    } else {
      this.#error = settings.error;
    }

    // The usage screen is worth less than the form: a failure to read it must
    // not stop somebody connecting a model.
    if (usage.ok) {
      this.#usage = toUsage(usage.value);
    }

    this.#loading = false;
  }

  /**
   * Saves the form.
   *
   * Everything at once: connections and uses arrive together because a use
   * pointing at a connection that did not save is a state nobody should be able
   * to reach. Each connection carries its own three-state key — omitted keeps
   * the stored one, empty removes it, a value replaces it.
   */
  async save(next: Assistance): Promise<AppError | null> {
    const before = this.#settings;

    this.#saving = true;
    this.#error = null;
    this.#settings = next;

    const outcome = await request(() =>
      http.PUT('/api/v1/settings/assistance', {
        body: {
          enabled: next.enabled,
          connections: next.connections.map((one) => ({
            provider: one.provider,
            apiKey: one.apiKey,
            baseUrl: one.baseUrl
          })),
          uses: next.uses.map((one) => ({
            capability: one.capability,
            enabled: one.enabled,
            provider: one.provider,
            model: one.model
          })),
          monthlyBudget: next.monthlyBudget,
          personalBudget: next.personalBudget
        }
      })
    );

    this.#saving = false;

    if (outcome.ok) {
      this.#settings = toAssistance(outcome.value);

      return null;
    }

    // Put back exactly what was there. A half-applied connection on screen is
    // worse than the change not having happened.
    this.#settings = before;
    this.#error = outcome.error;

    return outcome.error;
  }

  /**
   * Asks the providers what they offer, on opening the screen and again after
   * a key or an address has changed.
   *
   * A failure is not an error on this screen. Without the lists the model
   * pickers become text boxes, which is how this worked before and is still
   * usable.
   */
  async refreshModels(): Promise<void> {
    this.#listing = true;

    const models = await request(() => http.GET('/api/v1/settings/assistance/models'));

    if (models.ok) {
      this.#models = toProviderModels(models.value);
    }

    this.#listing = false;
  }

  reset(): void {
    this.#settings = null;
    this.#usage = null;
    this.#models = [];
    this.#loading = false;
    this.#listing = false;
    this.#saving = false;
    this.#error = null;
  }
}

function toUsage(wire: UsageWire): Usage {
  return {
    since: wire.since,
    totalCost: wire.totalCost,
    monthlyBudget: wire.monthlyBudget ?? null,
    totalInputTokens: wire.totalInputTokens,
    totalOutputTokens: wire.totalOutputTokens,
    totalPictures: wire.totalPictures,
    unpriced: wire.unpriced,
    byPerson: wire.byPerson.map((person) => ({
      userId: person.userId,
      displayName: person.displayName,
      calls: person.calls,
      cost: person.cost
    })),
    byCapability: wire.byCapability.map((one) => ({
      capability: one.capability,
      calls: one.calls,
      cost: one.cost
    }))
  };
}

export const createAssistanceStore = (): AssistanceStore => new AssistanceStore();
export const assistance = createAssistanceStore();

registerStore(() => assistance.reset());
