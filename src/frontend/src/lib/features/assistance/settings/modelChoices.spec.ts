import { describe, expect, it } from 'vitest';

import type { Assistance, ProviderModels, Use } from '../types';
import {
  budget,
  catalogueProblem,
  keyNeededAgain,
  modelsFor,
  offersWholeCatalogue,
  providerChoices,
  unlistedByProvider
} from './modelChoices';

const use = (overrides: Partial<Use> = {}): Use => ({
  capability: 'draft',
  enabled: true,
  provider: 'openai',
  model: '',
  defaultModel: 'gpt-default',
  ...overrides
});

const listing = (overrides: Partial<ProviderModels> = {}): ProviderModels => ({
  provider: 'openai',
  reachable: true,
  problem: null,
  models: [
    { id: 'writer', label: 'Writer', canDraw: false },
    { id: 'painter', label: 'Painter', canDraw: true }
  ],
  ...overrides
});

describe('providerChoices', () => {
  it('offers "not offered" first, and no drawing to a provider that cannot draw', () => {
    expect(providerChoices('draft').map((one) => one.value)).toEqual([
      '',
      'gemini',
      'openai',
      'ollama'
    ]);
    expect(providerChoices('draw').map((one) => one.value)).toEqual(['', 'gemini', 'openai']);
  });
});

describe('modelsFor', () => {
  it('keeps the default reachable and offers only the kind the job needs', () => {
    const writing = modelsFor([listing()], 'draft', use())!;
    const drawing = modelsFor([listing()], 'draw', use({ capability: 'draw' }))!;

    expect(writing.map((one) => one.value)).toEqual(['', 'writer']);
    expect(writing[0]!.label).toContain('gpt-default');
    expect(drawing.map((one) => one.value)).toEqual(['', 'painter']);
  });

  it('falls back to the whole catalogue when the filter leaves nothing', () => {
    const models = [listing({ models: [{ id: 'writer', label: 'Writer', canDraw: false }] })];
    const choices = modelsFor(models, 'draw', use({ capability: 'draw' }))!;

    expect(choices.map((one) => one.value)).toEqual(['', 'writer']);
    expect(offersWholeCatalogue(models, 'draw', use({ capability: 'draw' }))).toBe(true);
    expect(offersWholeCatalogue(models, 'draft', use())).toBe(false);
  });

  it.each([
    ['not listed yet', []],
    ['unreachable', [listing({ reachable: false, problem: 'assistance.unavailable' })]],
    ['empty', [listing({ models: [] })]]
  ])('has no list when the provider is %s', (_, models) => {
    expect(modelsFor(models, 'draft', use())).toBeNull();
  });

  it('has no list for a job with no provider', () => {
    expect(modelsFor([listing()], 'draft', use({ provider: '' }))).toBeNull();
  });
});

describe('catalogueProblem', () => {
  it('says nothing when the provider listed models, or was not asked', () => {
    expect(catalogueProblem([listing()], 'openai')).toBeNull();
    expect(catalogueProblem([], 'openai')).toBeNull();
  });

  it('tells a refused key from a provider that is down', () => {
    const rejected = catalogueProblem(
      [listing({ reachable: false, problem: 'assistance.rejected' })],
      'openai'
    );
    const down = catalogueProblem(
      [listing({ reachable: false, problem: 'assistance.unavailable' })],
      'openai'
    );

    expect(rejected).toBeTruthy();
    expect(down).toBeTruthy();
    expect(rejected).not.toBe(down);
  });

  it('names an empty catalogue, and marks the job as unlisted by it', () => {
    const models = [listing({ models: [] })];

    expect(catalogueProblem(models, 'openai')).toBeTruthy();
    expect(unlistedByProvider(models, use())).toBe(true);
    expect(unlistedByProvider(models, use({ provider: '' }))).toBe(false);
  });
});

describe('keyNeededAgain', () => {
  const saved = {
    connections: [
      { provider: 'openai', apiKeyConfigured: true, baseUrl: 'https://a.example', usable: true }
    ]
  } as Assistance;
  const edited = { ...saved.connections[0]! };

  it('asks again when the address changed and the stored key is kept', () => {
    expect(keyNeededAgain(saved, { ...edited, baseUrl: 'https://b.example' })).toBe(true);
  });

  it('does not when the address is unchanged, or a new key is being typed', () => {
    expect(keyNeededAgain(saved, edited)).toBe(false);
    expect(keyNeededAgain(saved, { ...edited, baseUrl: 'https://b.example', apiKey: 'k' })).toBe(
      false
    );
  });

  it('does not when no key is stored', () => {
    const none = { connections: [{ ...edited, apiKeyConfigured: false }] } as Assistance;

    expect(keyNeededAgain(none, { ...edited, baseUrl: 'https://b.example' })).toBe(false);
    expect(keyNeededAgain(null, edited)).toBe(false);
  });
});

describe('budget', () => {
  it('reads either decimal separator, and blank or words as no limit', () => {
    expect(budget('12,5')).toBe(12.5);
    expect(budget('20')).toBe(20);
    expect(budget('  ')).toBeNull();
    expect(budget('lots')).toBeNull();
  });
});
