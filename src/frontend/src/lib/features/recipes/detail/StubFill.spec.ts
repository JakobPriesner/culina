import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import type { Recipe } from '../types';
import StubFill from './StubFill.svelte';

/* A planned title, filled in where it is: the same recipe, so the plan and the notes keep pointing at it. */
const stub = {
  id: 'r1',
  householdId: 'h1',
  title: 'Linsen-Dal',
  language: 'de',
  yieldAmount: 2,
  yieldKind: 'servings',
  groups: [],
  steps: [],
  tags: []
} as unknown as Recipe;

afterEach(() => vi.unstubAllGlobals());

describe('filling a recipe that is only a title', () => {
  it('turns pasted text into this recipe’s ingredients and steps', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(JSON.stringify({ own: [] }), { status: 200 })))
    );
    const onfill = vi.fn();

    renderWithProviders(StubFill, { props: { recipe: stub, onfill } });

    await userEvent.click(screen.getByRole('button', { name: 'Paste text or a link' }));
    await userEvent.type(
      screen.getByRole('textbox', { name: 'The recipe, as text' }),
      'Linsen-Dal{enter}Zutaten{enter}200 g rote Linsen{enter}Zubereitung{enter}1. Linsen kochen.'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Save and edit recipe' }));

    const [patch] = onfill.mock.lastCall as [Partial<Recipe>];

    expect(patch.groups?.[0]?.ingredients[0]?.name).toMatch(/Linsen/);
    expect(patch.steps).toHaveLength(1);
  });
});
