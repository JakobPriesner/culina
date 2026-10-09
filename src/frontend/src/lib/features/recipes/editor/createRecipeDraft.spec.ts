import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import type { Recipe } from '$features/recipes/types';

import { adoptSaved, createRecipeDraft } from './createRecipeDraft.svelte';

const butter = { id: 'i-butter', name: 'butter', note: null, quantity: { value: 200, unit: 'g' } };
const saffron = { id: '', name: 'Saffron', note: null, quantity: { value: null, unit: null } };

const opened: Recipe = {
  id: 'recipe-1',
  householdId: 'household-1',
  title: 'Risotto',
  description: null,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [{ id: 'g-1', name: null, ingredients: [butter] }],
  steps: [{ id: 's-1', title: null, segments: [], uses: [], durationSeconds: null }],
  tags: [],
  sourceUrl: null,
  createdBy: 'user-1',
  createdAt: '2026-09-20T12:00:00Z',
  updatedAt: '2026-09-20T12:00:00Z',
  version: 1
};

describe('adoptSaved', () => {
  it('gives a new line the id the server assigned, matched by name', () => {
    const draft = { ...opened, groups: [{ ...opened.groups[0]!, ingredients: [butter, saffron] }] };
    const saved = {
      ...opened,
      version: 2,
      groups: [{ ...opened.groups[0]!, ingredients: [butter, { ...saffron, id: 'i-saffron' }] }]
    };

    const { recipe } = adoptSaved(draft, saved, 1);

    expect(recipe.groups[0]!.ingredients.map((one) => one.id)).toEqual(['i-butter', 'i-saffron']);
  });

  it('gives two lines of one name an id each', () => {
    const draft = {
      ...opened,
      groups: [{ ...opened.groups[0]!, ingredients: [saffron, { ...saffron }] }]
    };
    const saved = {
      ...opened,
      groups: [
        {
          ...opened.groups[0]!,
          ingredients: [
            { ...saffron, id: 'a' },
            { ...saffron, id: 'b' }
          ]
        }
      ]
    };

    const { recipe } = adoptSaved(draft, saved, 1);

    expect(recipe.groups[0]!.ingredients.map((one) => one.id)).toEqual(['a', 'b']);
  });

  it('gives a new step the id the server assigned, leaving steps that have one', () => {
    const added = { id: '', title: null, segments: [], uses: [], durationSeconds: null };
    const draft = { ...opened, steps: [opened.steps[0]!, added] };
    const saved = { ...opened, steps: [opened.steps[0]!, { ...added, id: 's-2' }] };

    const { recipe, linked } = adoptSaved(draft, saved, 1);

    expect(recipe.steps.map((one) => one.id)).toEqual(['s-1', 's-2']);
    expect(linked).toBe(false);
  });

  it('does not give a new step an id another step in the draft already holds', () => {
    const added = { id: '', title: null, segments: [], uses: [], durationSeconds: null };
    const draft = { ...opened, steps: [added, opened.steps[0]!] };

    expect(adoptSaved(draft, opened, 1).recipe.steps.map((one) => one.id)).toEqual(['', 's-1']);
  });

  it('keeps steps taken while the save was in the air on top of the new version', () => {
    const draft = { ...opened, version: 3 };
    const saved = { ...opened, version: 2 };

    expect(adoptSaved(draft, saved, 1).recipe.version).toBe(4);
  });

  it('reports nothing to link when no step mentions a new line', () => {
    expect(adoptSaved(opened, opened, 1).linked).toBe(false);
  });
});

describe('createRecipeDraft', () => {
  let stored: Recipe;

  beforeEach(() => {
    vi.useFakeTimers();
    localStorage.clear();
    stored = opened;

    vi.spyOn(session, 'user', 'get').mockReturnValue({ userId: 'user-1' } as never);
    vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('household-1');
    vi.spyOn(recipes, 'detail', 'get').mockImplementation(() => stored);
    vi.spyOn(recipes, 'load').mockResolvedValue();
    vi.spyOn(recipes, 'update').mockImplementation((next) => {
      stored = { ...next, version: next.version + 1 };

      return Promise.resolve(null);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('opens on the loaded recipe once, and not again over what was typed', () => {
    const editor = createRecipeDraft(() => 'recipe-1');

    editor.takeLoaded();
    editor.change({ title: 'Paella' });
    editor.takeLoaded();

    expect(editor.recipe?.title).toBe('Paella');
    editor.dispose();
  });

  it('says where the work is: here until saved, then saved', async () => {
    const editor = createRecipeDraft(() => 'recipe-1');

    expect(editor.saveState.tone).toBe('idle');

    editor.takeLoaded();
    editor.change({ title: 'Paella' });

    expect(editor.saveState.tone).toBe('local');

    await vi.advanceTimersByTimeAsync(1000);

    expect(recipes.update).toHaveBeenCalledOnce();
    expect(editor.saveState.tone).toBe('saved');
    expect(editor.recipe?.version).toBe(2);
    editor.dispose();
  });

  it('opens on what this device kept in place of the server copy', () => {
    localStorage.setItem(
      'culina.draft.user-1.recipe-1',
      JSON.stringify({ recipe: { ...opened, title: 'Kept' }, at: '2026-09-21T00:00:00Z' })
    );

    const editor = createRecipeDraft(() => 'recipe-1');

    editor.takeLoaded();

    expect(editor.recipe?.title).toBe('Kept');
    expect(editor.saveState.tone).toBe('local');
    editor.dispose();
  });

  it('moves the version on one step when a photo is written', () => {
    const editor = createRecipeDraft(() => 'recipe-1');

    editor.takeLoaded();
    editor.photoWritten('image-1');

    expect(editor.recipe).toMatchObject({ imageId: 'image-1', version: 2 });
    editor.dispose();
  });

  it('keeps what was typed in a number field until it parses', () => {
    const editor = createRecipeDraft(() => 'recipe-1');

    editor.takeLoaded();
    editor.writeYield('');
    editor.writeYield('1,');
    editor.writeYield('12');
    editor.writeMinutes('prepMinutes', '10');
    editor.writeMinutes('prepMinutes', '');

    expect(editor.recipe).toMatchObject({ yieldAmount: 12, prepMinutes: null });
    expect(editor.typed).toMatchObject({ yieldAmount: '12', prepMinutes: '' });
    editor.dispose();
  });

  it('is not offered to a household that only inherits the recipe', () => {
    vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('household-2');

    const editor = createRecipeDraft(() => 'recipe-1');

    editor.takeLoaded();

    expect(editor.inheritedFrom).toBe('household-1');
    expect(editor.recipe).toBeNull();
    editor.dispose();
  });
});
