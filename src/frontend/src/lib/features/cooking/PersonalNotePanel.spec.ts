import { screen, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import PersonalNotePanel from './PersonalNotePanel.svelte';
import { notes } from './stores/notes.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * Typing into a note that has not arrived yet loses the typing: the answer
 * replaces it, and the next save sends the emptiness back as the note.
 */
afterEach(() => {
  notes.reset();
  vi.unstubAllGlobals();
});

describe('your note on a recipe', () => {
  it('cannot be typed into until it has been read', async () => {
    let answer: ((response: Response) => void) | undefined;

    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise<Response>((resolve) => (answer = resolve)))
    );

    renderWithProviders(PersonalNotePanel, { props: { recipeId: 'r1', variant: 'cook' } });

    const note = screen.getByRole('textbox');

    await waitFor(() => expect(answer).toBeDefined());
    expect(note).toHaveAttribute('readonly');

    answer?.(
      new Response(JSON.stringify({ overall: 'Use the heavy pan', steps: [] }), {
        headers: { 'Content-Type': 'application/json' }
      })
    );

    await waitFor(() => expect(note).not.toHaveAttribute('readonly'));
    expect(note).toHaveValue('Use the heavy pan');
  });
});
