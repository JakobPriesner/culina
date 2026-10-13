import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import CookbookSheet from './CookbookSheet.svelte';
import type { CookbookDetail } from './types';

const cookbook: CookbookDetail = {
  id: 'c1',
  householdId: 'h1',
  name: 'Sonntag',
  description: null,
  kind: 'manual',
  rules: null,
  recipeCount: 3,
  cover: [],
  coverRecipeId: null,
  updatedAt: '2026-10-01T00:00:00Z',
  version: 2
};

const recipe = (recipeId: string, title: string, imageId: string | null) => ({
  recipeId,
  householdId: 'h1',
  title,
  imageId,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  updatedAt: '2026-10-01T00:00:00Z'
});

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
  };
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            items: [
              recipe('r1', 'Braten', 'i1'),
              recipe('r2', 'Suppe', null),
              recipe('r3', 'Kuchen', 'i3')
            ],
            total: 3,
            nextCursor: null
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      )
    )
  );
});

describe('choosing a cover', () => {
  it('offers the photographed recipes besides the first photos, and saves the one chosen', async () => {
    const onsave = vi.fn();
    const user = userEvent.setup();

    renderWithProviders(CookbookSheet, {
      props: { open: true, householdId: 'h1', cookbook, onsave, onclose: vi.fn() }
    });

    const kuchen = await screen.findByRole('radio', { name: 'Kuchen' });

    expect(screen.getByRole('radio', { name: 'First photos' })).toBeChecked();
    expect(screen.queryByRole('radio', { name: 'Suppe' })).not.toBeInTheDocument();

    await user.click(kuchen);
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onsave).toHaveBeenCalledWith('Sonntag', null, null, 'r3');
  });

  it('asks nothing about a cover for a new cookbook, which has nothing on it', async () => {
    renderWithProviders(CookbookSheet, {
      props: { open: true, householdId: 'h1', onsave: vi.fn(), onclose: vi.fn() }
    });

    expect(await screen.findByRole('textbox', { name: /name/i })).toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: 'First photos' })).not.toBeInTheDocument();
  });
});
