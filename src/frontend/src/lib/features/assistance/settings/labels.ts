import { formatDate, m } from '$shell/i18n';

import type { Capability, PricedBy, Provider } from '../types';

// Static maps, not computed `m[...]` keys, so Paraglide can tree-shake unused messages.

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

/** The day Culina's price list was recorded, as a date: a calendar day, so read and shown in UTC. */
export const recordedOn = (day: string): string =>
  formatDate(new Date(`${day}T00:00:00Z`), { dateStyle: 'long', timeZone: 'UTC' });

/** Where a cost figure's price came from, in words. */
export function pricedByLabel(pricedBy: PricedBy, pricesRecordedOn: string): string {
  if (pricedBy === 'administrator') return m['ai.usage.pricedBy.administrator']();
  if (pricedBy === 'culina') {
    return m['ai.usage.pricedBy.culina']({ date: recordedOn(pricesRecordedOn) });
  }

  return m['ai.usage.pricedBy.none']();
}
