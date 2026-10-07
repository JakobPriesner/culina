import type { components } from '$api/generated/schema';

/**
 * Provider facts live here, not on the server: they hold for the product everywhere (Ollama is local: no key, no pictures).
 * The default model is deliberately absent; the server sends it so a form never shows a stale placeholder.
 */
export const providers = ['gemini', 'openai', 'ollama'] as const;

export type Provider = (typeof providers)[number];

export const capabilities = ['improve', 'draft', 'read', 'draw'] as const;

export type Capability = (typeof capabilities)[number];

interface ProviderFacts {
  /** Whether connecting needs a credential. */
  needsApiKey: boolean;
  /** Whether it must be told where it is: hosted providers share one address, a local one is the connection, so the form requires it. */
  needsAddress: boolean;
  canDraw: boolean;
  /** Hint for an empty address box. */
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

export const providersFor = (capability: Capability): readonly Provider[] =>
  capability === 'draw' ? providers.filter((one) => providerFacts[one].canDraw) : providers;

export interface Connection {
  provider: Provider;
  /** Whether a key is stored; never the key itself. */
  apiKeyConfigured: boolean;
  baseUrl: string;
  usable: boolean;
  /** A newly typed key: `undefined` keeps the stored one, `''` removes it, a value replaces it. Never sent by the server. */
  apiKey?: string;
}

export interface Use {
  capability: Capability;
  enabled: boolean;
  /** Empty when nothing was chosen: the job is not offered. */
  provider: Provider | '';
  /** Empty for the server's current default. */
  model: string;
  /** The server's default, used when the model is empty. */
  defaultModel: string;
}

export interface Assistance {
  enabled: boolean;
  connections: Connection[];
  uses: Use[];
  monthlyBudget: number | null;
  personalBudget: number | null;
}

export interface Model {
  id: string;
  label: string;
  /** Whether it makes pictures, read from the name by the server. */
  canDraw: boolean;
}

export interface ProviderModels {
  provider: Provider;
  reachable: boolean;
  /** An error code when it did not answer; the client has the words. */
  problem: string | null;
  models: Model[];
}

export interface Usage {
  since: string;
  totalCost: number;
  monthlyBudget: number | null;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalPictures: number;
  /** Calls with a model this app has no price for. */
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
