import type { components } from '$api/generated/schema';

/**
 * What the assistant is, on this instance.
 *
 * The provider facts live here rather than being asked of the server, because
 * they are facts about the products rather than about this installation: Ollama
 * runs on your own machine, so it has no key and no picture-drawing, and no
 * amount of configuring changes that. A round trip to learn it would be a round
 * trip to be told something that is true everywhere.
 *
 * What is *not* here is which model each provider defaults to. That is the
 * server's to decide and the server sends it, so a form cannot show a
 * placeholder the server has stopped agreeing with.
 */
export const providers = ['gemini', 'openai', 'ollama'] as const;

export type Provider = (typeof providers)[number];

/** Which of the four jobs the assistant can be given. */
export const capabilities = ['improve', 'draft', 'read', 'draw'] as const;

export type Capability = (typeof capabilities)[number];

interface ProviderFacts {
  /** Whether connecting means giving it a credential. */
  needsApiKey: boolean;
  /**
   * Whether it has to be told where it is.
   *
   * The hosted two have one address between all their customers. A local one is
   * wherever you put it, so the address is the connection rather than an
   * override of it — and the form must require it rather than offer it.
   */
  needsAddress: boolean;
  /** Whether it can draw at all. */
  canDraw: boolean;
  /** What to put in the address box when it is empty, as a hint only. */
  addressHint: string;
}

export const providerFacts: Record<Provider, ProviderFacts> = {
  gemini: {
    needsApiKey: true,
    needsAddress: false,
    canDraw: true,
    addressHint: 'https://generativelanguage.googleapis.com'
  },
  openai: {
    needsApiKey: true,
    needsAddress: false,
    canDraw: true,
    addressHint: 'https://api.openai.com'
  },
  ollama: {
    needsApiKey: false,
    needsAddress: true,
    canDraw: false,
    addressHint: 'http://localhost:11434'
  }
};

/** Whether a name from the server is one this build knows how to draw a form for. */
export function isProvider(value: string): value is Provider {
  return (providers as readonly string[]).includes(value);
}

/** Which providers could do this job at all. */
export const providersFor = (capability: Capability): readonly Provider[] =>
  capability === 'draw' ? providers.filter((one) => providerFacts[one].canDraw) : providers;

/** One provider, as the settings screen works in it. */
export interface Connection {
  provider: Provider;
  /** Whether a key is stored. Never the key: no endpoint returns it. */
  apiKeyConfigured: boolean;
  baseUrl: string;
  /** Whether this connection has everything its provider needs. */
  usable: boolean;
  /**
   * A new key typed into the form, if one was.
   *
   * Three states, matching what the endpoint distinguishes: `undefined` leaves
   * the stored key alone, `''` removes it, and a value replaces it. Not part of
   * what the server sends, because the server never sends a key.
   */
  apiKey?: string;
}

/** What does one job. */
export interface Use {
  capability: Capability;
  enabled: boolean;
  /** Empty when nothing was chosen, which means the job is not offered. */
  provider: Provider | '';
  /** Empty for the server's current default, which it sends below. */
  model: string;
  /** What the server would use if the model above is left empty. */
  defaultModel: string;
}

/** How the assistant is set up. */
export interface Assistance {
  enabled: boolean;
  connections: Connection[];
  uses: Use[];
  monthlyBudget: number | null;
  personalBudget: number | null;
}

/** One model a provider offers. */
export interface Model {
  id: string;
  label: string;
  /** Whether it makes pictures, as the server read it from the name. */
  canDraw: boolean;
}

/** What one provider offers, or why it offered nothing. */
export interface ProviderModels {
  provider: Provider;
  reachable: boolean;
  /** An error code when it did not answer. The client has the words. */
  problem: string | null;
  models: Model[];
}

/** What it has cost this month. */
export interface Usage {
  since: string;
  totalCost: number;
  monthlyBudget: number | null;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalPictures: number;
  /** Calls made with a model this app has no price for. */
  unpriced: number;
  byPerson: PersonUsage[];
  byCapability: CapabilityUsage[];
}

export interface PersonUsage {
  userId: string;
  displayName: string;
  calls: number;
  cost: number;
}

export interface CapabilityUsage {
  capability: string;
  calls: number;
  cost: number;
}

type AssistanceWire = components['schemas']['SettingsGetAssistanceResponse'];
type ModelsWire = components['schemas']['SettingsGetAssistanceModelsResponse'];

/** Reads what each provider offers, dropping any this build cannot draw a row for. */
export function toProviderModels(wire: ModelsWire): ProviderModels[] {
  return wire.providers
    .filter((one) => isProvider(one.provider))
    .map((one) => ({
      provider: one.provider as Provider,
      reachable: one.reachable,
      problem: one.problem ?? null,
      models: one.models.map((model) => ({
        id: model.id,
        label: model.label,
        canDraw: model.canDraw
      }))
    }));
}

/** Reads what the server sent, dropping anything this build cannot draw. */
export function toAssistance(wire: AssistanceWire): Assistance {
  return {
    enabled: wire.enabled,
    connections: wire.connections
      .filter((one) => isProvider(one.provider))
      .map((one) => ({
        provider: one.provider as Provider,
        apiKeyConfigured: one.apiKeyConfigured,
        baseUrl: one.baseUrl,
        usable: one.usable
      })),
    uses: wire.uses
      .filter((one) => (capabilities as readonly string[]).includes(one.capability))
      .map((one) => ({
        capability: one.capability as Capability,
        enabled: one.enabled,
        provider: isProvider(one.provider) ? one.provider : '',
        model: one.model,
        defaultModel: one.defaultModel
      })),
    monthlyBudget: wire.monthlyBudget ?? null,
    personalBudget: wire.personalBudget ?? null
  };
}
