import { http, request, type AppError } from '$api';
import { registerStore } from '$shell/stores';

import { isProvider, type Assistance, type Usage } from '../types';

import type { components } from '$api/generated/schema';

type AssistanceWire = components['schemas']['SettingsGetAssistanceResponse'];
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
  #loading = $state(false);
  #saving = $state(false);
  #error = $state<AppError | null>(null);

  get settings(): Assistance | null {
    return this.#settings;
  }

  get usage(): Usage | null {
    return this.#usage;
  }

  get loading(): boolean {
    return this.#loading;
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
   * @param next What the form holds now.
   * @param apiKey A new key, `''` to clear the stored one, or `undefined` to
   *   leave it alone — the three states the endpoint distinguishes, passed
   *   through rather than folded into `next`, because the key is the one value
   *   that is never part of what was read.
   */
  async save(next: Assistance, apiKey: string | undefined): Promise<AppError | null> {
    const before = this.#settings;

    this.#saving = true;
    this.#error = null;
    this.#settings = next;

    const outcome = await request(() =>
      http.PUT('/api/v1/settings/assistance', {
        body: {
          enabled: next.enabled,
          provider: next.provider,
          apiKey,
          baseUrl: next.baseUrl,
          composeModel: next.composeModel,
          drawModel: next.drawModel,
          improveEnabled: next.improveEnabled,
          draftEnabled: next.draftEnabled,
          readEnabled: next.readEnabled,
          drawEnabled: next.drawEnabled,
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

  reset(): void {
    this.#settings = null;
    this.#usage = null;
    this.#loading = false;
    this.#saving = false;
    this.#error = null;
  }
}

function toAssistance(wire: AssistanceWire): Assistance {
  return {
    enabled: wire.enabled,
    // A build that has not heard of the stored provider still has to render
    // something. Falling back keeps the form usable instead of blank, and the
    // server refuses anything it does not know on the way back in.
    provider: isProvider(wire.provider) ? wire.provider : 'openai',
    apiKeyConfigured: wire.apiKeyConfigured,
    connected: wire.connected,
    baseUrl: wire.baseUrl,
    composeModel: wire.composeModel,
    drawModel: wire.drawModel,
    improveEnabled: wire.improveEnabled,
    draftEnabled: wire.draftEnabled,
    readEnabled: wire.readEnabled,
    drawEnabled: wire.drawEnabled,
    monthlyBudget: wire.monthlyBudget ?? null,
    personalBudget: wire.personalBudget ?? null
  };
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
