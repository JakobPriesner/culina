import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';

import NotePeek from './NotePeek.svelte';
import { notes } from './stores/notes.svelte';

/* One place for your note on the recipe page: read near the top, and written there too. */
const answer = (overall: string | null) =>
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ overall, steps: [] }), {
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
  );

afterEach(() => {
  notes.reset();
  vi.unstubAllGlobals();
});

describe('your note near the top of the recipe', () => {
  it('is read there, and written there on a tap', async () => {
    answer('Less sugar.');
    await notes.load('r1');

    renderWithProviders(NotePeek, { props: { recipeId: 'r1' } });

    expect(screen.getByText('Less sugar.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Edit your note' }));

    expect(screen.getByRole('textbox', { name: 'Your notes' })).toHaveValue('Less sugar.');
  });

  it('offers to start one when there is none', async () => {
    answer(null);
    await notes.load('r1');

    renderWithProviders(NotePeek, { props: { recipeId: 'r1' } });

    await userEvent.click(screen.getByRole('button', { name: /Add a note for next time/ }));

    expect(screen.getByRole('textbox', { name: 'Your notes' })).toHaveFocus();
  });
});
