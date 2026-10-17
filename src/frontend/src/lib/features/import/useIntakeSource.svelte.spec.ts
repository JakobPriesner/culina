import { flushSync } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { intakes, type IntakeJob } from './intakes.svelte';
import { useIntakeSource } from './useIntakeSource.svelte';

/* The editor keeps the original beside the recipe, and opening it is the review. */
const job = (stage: string, recipeId = 'r1') =>
  ({
    id: 'i1',
    stage,
    recipeId,
    householdId: 'h',
    createdAt: '2026-01-01T00:00:00Z',
    material: 'Spaghetti, 250 g',
    photoCount: 0
  }) as IntakeJob;

let cleanup: () => void = () => {};

function open(intakeId: string | null) {
  let source!: ReturnType<typeof useIntakeSource>;

  cleanup = $effect.root(() => {
    source = useIntakeSource({ recipeId: () => 'r1', intakeId: () => intakeId });
  });
  flushSync();

  return source;
}

beforeEach(() => {
  intakes.own(null);
  intakes.own('person');
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('the original beside the editor', () => {
  it('finds the ready import of the recipe and marks it reviewed, keeping the original on screen', async () => {
    intakes.jobs = [job('ready')];
    const reviewed = vi.spyOn(intakes, 'reviewed').mockImplementation(async () => {
      intakes.jobs = [];
      return true;
    });

    const source = open(null);
    await Promise.resolve();
    flushSync();

    expect(reviewed).toHaveBeenCalledExactlyOnceWith('i1');
    expect(source.job?.material).toBe('Spaghetti, 250 g');
  });

  it('asks the server once for the import named in the address', () => {
    const get = vi.spyOn(intakes, 'get').mockImplementation(async () => {
      intakes.jobs = [job('reviewed')];
      return job('reviewed');
    });
    const reviewed = vi.spyOn(intakes, 'reviewed');

    const source = open('i1');

    expect(get).toHaveBeenCalledExactlyOnceWith('i1');

    return vi.waitFor(() => {
      expect(source.job?.id).toBe('i1');
      expect(reviewed).not.toHaveBeenCalled();
    });
  });

  it('shows nothing for an import that made another recipe', () => {
    intakes.jobs = [job('reviewed', 'r2')];

    expect(open('i1').job).toBeNull();
  });
});
