import { m } from '$shell/i18n';

import type { Capability, Provider } from '../types';

/*
 * Static maps rather than `m[`ai.provider.${provider}`]`: a computed key makes
 * every message reachable, so Paraglide cannot drop the ones that are never
 * used and they all ship in one shared chunk. Every key is named here instead.
 */

const providerNames: Record<Provider, () => string> = {
  gemini: () => m['ai.provider.gemini'](),
  openai: () => m['ai.provider.openai'](),
  ollama: () => m['ai.provider.ollama']()
};

const jobLabels: Record<Capability, () => string> = {
  improve: () => m['ai.improve'](),
  draft: () => m['ai.draft'](),
  read: () => m['ai.read'](),
  draw: () => m['ai.draw']()
};

const jobHints: Record<Capability, () => string> = {
  improve: () => m['ai.improve.hint'](),
  draft: () => m['ai.draft.hint'](),
  read: () => m['ai.read.hint'](),
  draw: () => m['ai.draw.hint']()
};

export const providerName = (provider: Provider): string => providerNames[provider]();

export const jobLabel = (capability: Capability): string => jobLabels[capability]();

export const jobHint = (capability: Capability): string => jobHints[capability]();
