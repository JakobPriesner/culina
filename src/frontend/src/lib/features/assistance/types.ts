/**
 * What the assistant is, on this instance.
 *
 * The provider facts live here rather than being asked of the server, because
 * they are facts about the products rather than about this installation: Ollama
 * runs on your own machine, so it has no key and no picture-drawing, and no
 * amount of configuring changes that. A round trip to learn it would be a round
 * trip to be told something that is true everywhere.
 */
export const providers = ['gemini', 'openai', 'ollama'] as const;

export type Provider = (typeof providers)[number];

/** Which of the four things the assistant can be asked to do. */
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
  /** A model that is a sensible starting point, as a hint only. */
  composeHint: string;
  /** A picture model that is a sensible starting point, as a hint only. */
  drawHint: string;
}

export const providerFacts: Record<Provider, ProviderFacts> = {
  gemini: {
    needsApiKey: true,
    needsAddress: false,
    canDraw: true,
    addressHint: 'https://generativelanguage.googleapis.com',
    composeHint: 'gemini-2.5-flash',
    drawHint: 'gemini-2.5-flash-image'
  },
  openai: {
    needsApiKey: true,
    needsAddress: false,
    canDraw: true,
    addressHint: 'https://api.openai.com',
    composeHint: 'gpt-4o-mini',
    drawHint: 'gpt-image-1'
  },
  ollama: {
    needsApiKey: false,
    needsAddress: true,
    canDraw: false,
    addressHint: 'http://localhost:11434',
    composeHint: 'llama3.2',
    drawHint: ''
  }
};

/** Whether a name from the server is one this build knows how to draw a form for. */
export function isProvider(value: string): value is Provider {
  return (providers as readonly string[]).includes(value);
}

/** How the assistant is set up, as the settings screen works in it. */
export interface Assistance {
  enabled: boolean;
  provider: Provider;
  /** Whether a key is stored. Never the key: no endpoint returns it. */
  apiKeyConfigured: boolean;
  /** Whether the connection has everything this provider needs. */
  connected: boolean;
  baseUrl: string;
  composeModel: string;
  drawModel: string;
  improveEnabled: boolean;
  draftEnabled: boolean;
  readEnabled: boolean;
  drawEnabled: boolean;
  monthlyBudget: number | null;
  personalBudget: number | null;
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
