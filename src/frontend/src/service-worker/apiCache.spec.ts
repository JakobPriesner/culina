import { describe, expect, it, vi } from 'vitest';

vi.mock('$service-worker', () => ({ base: '', version: 'test' }));
vi.mock('../lib/features/cooking/timerState', () => ({ forgetKitchen: vi.fn() }));

const { policyFor } = await import('./apiCache');

const policy = (path: string) => policyFor(new URL(path, 'https://culina.test'));

/* The private cache holds exact shapes only: a route joins it on purpose, never by a pattern that happens to match. */
describe('what the service worker keeps for offline reading', () => {
  it('answers a recipe and its nutrition network-first', () => {
    expect(policy('/api/v1/recipes/abc')).toBe('network-first');
    expect(policy('/api/v1/recipes/abc/nutrition')).toBe('network-first');
    expect(policy('/api/v1/recipes/abc/nutrition?householdId=h1')).toBe('network-first');
  });

  it('keeps nothing else under a recipe', () => {
    expect(policy('/api/v1/recipes/abc/notes')).toBeNull();
    expect(policy('/api/v1/recipes/abc/related')).toBeNull();
    expect(policy('/api/v1/recipes/abc/nutrition/foods')).toBeNull();
  });
});
