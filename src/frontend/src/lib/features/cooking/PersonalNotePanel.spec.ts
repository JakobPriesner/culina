import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
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

  /*
   * An empty note invites typing, and the save would replace the note the
   * server still holds without it ever having been shown.
   */
  it('does not pass off a failed read as no note', async () => {
    const answers = [
      () => new Response(null, { status: 503 }),
      () =>
        new Response(JSON.stringify({ overall: 'Use the heavy pan', steps: [] }), {
          headers: { 'Content-Type': 'application/json' }
        })
    ];

    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(answers.shift()?.() ?? new Response(null, { status: 500 })))
    );

    renderWithProviders(PersonalNotePanel, { props: { recipeId: 'r1', variant: 'cook' } });

    const alert = await screen.findByRole('alert');

    expect(screen.queryByRole('textbox')).toBeNull();

    await userEvent.click(within(alert).getByRole('button'));

    await waitFor(() => expect(screen.getByRole('textbox')).toHaveValue('Use the heavy pan'));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
