import { providerFacts, type Price, type SetPrice, type Use } from '../types';

/** A model some job uses that a provider bills for: what the administrator might want to price. */
export interface PricedModel {
  model: string;
  /** Whether a text job uses it, so it is billed per token. */
  tokens: boolean;
  /** Whether the drawing job uses it, so it is billed per picture. */
  pictures: boolean;
  /** Culina's own price for it, or null when its list does not know the model. */
  listed: Price | null;
}

/** Model names are matched the way the server matches them: exactly, ignoring case and surrounding space. */
export const sameModel = (one: string, other: string): boolean =>
  one.trim().toLowerCase() === other.trim().toLowerCase();

/** The billed models the jobs use, once each; a local model costs nothing, so it has no row. */
export function pricedModels(uses: readonly Use[]): PricedModel[] {
  const found: PricedModel[] = [];

  for (const use of uses) {
    if (!use.enabled || use.provider === '' || !providerFacts[use.provider].needsApiKey) continue;

    const model = (use.model || use.defaultModel).trim();
    const drawing = use.capability === 'draw';

    if (model === '') continue;

    const known = found.find((one) => sameModel(one.model, model));

    if (known) {
      known.tokens ||= !drawing;
      known.pictures ||= drawing;
    } else {
      found.push({ model, tokens: !drawing, pictures: drawing, listed: use.listedPrice });
    }
  }

  return found;
}

/** Whether a price gives input tokens without output tokens, or the reverse; until both are there, Culina's token price is used. */
export const halfPriced = (price: Price): boolean =>
  (price.inputPerMillion === null) !== (price.outputPerMillion === null);

/** The administrator's price for a model, or an empty one (which keeps Culina's). */
export const priceFor = (prices: readonly SetPrice[], model: string): SetPrice =>
  prices.find((one) => sameModel(one.model, model)) ?? {
    model,
    inputPerMillion: null,
    outputPerMillion: null,
    perImage: null
  };
