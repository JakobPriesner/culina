import { describe, expect, it } from 'vitest';

import type { Use } from '../types';
import { halfPriced, priceFor, pricedModels } from './prices';

const use = (overrides: Partial<Use>): Use => ({
  capability: 'improve',
  enabled: true,
  provider: 'openai',
  model: '',
  defaultModel: 'gpt-6-astra',
  listedPrice: null,
  ...overrides
});

describe('pricedModels', () => {
  it('lists a model once however many jobs use it, by the name the server will record', () => {
    const models = pricedModels([
      use({ capability: 'improve' }),
      use({ capability: 'draft', model: 'GPT-6-Astra' }),
      use({ capability: 'draw', model: 'gpt-image-2.5-flare' })
    ]);

    expect(models.map((one) => [one.model, one.tokens, one.pictures])).toEqual([
      ['gpt-6-astra', true, false],
      ['gpt-image-2.5-flare', false, true]
    ]);
  });

  it('leaves out local models and jobs nobody switched on, since neither is billed', () => {
    expect(
      pricedModels([
        use({ provider: 'ollama', defaultModel: 'llama3.2' }),
        use({ enabled: false }),
        use({ provider: '' })
      ])
    ).toEqual([]);
  });
});

describe('priceFor', () => {
  it('finds the administrator’s price whatever the case, and an empty one otherwise', () => {
    const set = { model: 'gpt-6-astra', inputPerMillion: 1, outputPerMillion: 2, perImage: null };

    expect(priceFor([set], 'GPT-6-ASTRA')).toBe(set);
    expect(priceFor([set], 'gpt-7')).toEqual({
      model: 'gpt-7',
      inputPerMillion: null,
      outputPerMillion: null,
      perImage: null
    });
  });
});

describe('halfPriced', () => {
  it('is true only when one of input and output is missing', () => {
    const price = { inputPerMillion: null, outputPerMillion: null, perImage: 0.04 };

    expect(halfPriced(price)).toBe(false);
    expect(halfPriced({ ...price, inputPerMillion: 1 })).toBe(true);
    expect(halfPriced({ ...price, inputPerMillion: 1, outputPerMillion: 2 })).toBe(false);
  });
});
