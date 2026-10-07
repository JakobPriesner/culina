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

/** Assistant configuration and spend, admin-only and loaded on demand. The API key is never held: a save sends a new key or nothing. */
class AssistanceStore {
  #settings = $state<Assistance | null>(null);
  #usage = $state<Usage | null>(null);
  #models = $state<ProviderModels[]>([]);
  #loading = $state(false);
  #listing = $state(false);
  #unlisted = $state(false);
  #saving = $state(false);
  #error = $state<AppError | null>(null);

  get settings(): Assistance | null {
    return this.#settings;
  }

  get usage(): Usage | null {
    return this.#usage;
  }

  /** Models each connected provider offers; empty until asked or when a provider is unreachable (pickers fall back to text boxes). */
  get models(): ProviderModels[] {
    return this.#models;
  }

  get loading(): boolean {
    return this.#loading;
  }

  get listing(): boolean {
    return this.#listing;
  }

  /** Whether the listing request itself failed, as opposed to a provider answering "no". */
  get unlisted(): boolean {
    return this.#unlisted;
  }

  get saving(): boolean {
    return this.#saving;
  }

  get error(): AppError | null {
    return this.#error;
  }

  async load(): Promise<void> {
    this.#loading = true;
    this.#error = null;

    // Not awaited with the others: model lists are slow remote calls that would blank the whole screen, so only the pickers wait.
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

    // A usage failure must not stop somebody connecting a model.
    if (usage.ok) {
      this.#usage = toUsage(usage.value);
    }

    this.#loading = false;
  }

  /** Saves everything at once, since a use pointing at an unsaved connection must be unreachable. Key per connection: omitted keeps, empty removes, a value replaces. */
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

      // Saved keys and addresses decide who can be asked, so the lists are stale now.
      void this.refreshModels();

      return null;
    }

    // Restore the previous settings; a half-applied connection is worse than none.
    this.#settings = before;
    this.#error = outcome.error;

    return outcome.error;
  }

  /** Asks providers what they offer; a failure is not an error here, the pickers fall back to text boxes. */
  async refreshModels(): Promise<void> {
    this.#listing = true;

    const models = await request(() => http.GET('/api/v1/settings/assistance/models'));

    if (models.ok) {
      this.#models = toProviderModels(models.value);
    }

    this.#unlisted = !models.ok;
    this.#listing = false;
  }

  reset(): void {
    this.#settings = null;
    this.#usage = null;
    this.#models = [];
    this.#loading = false;
    this.#listing = false;
    this.#unlisted = false;
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
