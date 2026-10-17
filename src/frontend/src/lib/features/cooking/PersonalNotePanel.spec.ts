import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import PersonalNotePanel from './PersonalNotePanel.svelte';
import { notes } from './stores/notes.svelte';
import { renderWithProviders } from '$lib/test/render';

/* Typing into a note that has not arrived loses the typing: the answer replaces it and the next save sends the emptiness back.
   The page reads the note (the recipe page and cook mode both do), so each test asks for it as the page would. */
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

    // The page reads the note, as cook mode does when it opens.
    renderWithProviders(PersonalNotePanel, { props: { recipeId: 'r1', variant: 'cook' } });
    void notes.load('r1');

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

  /* An empty note invites typing, and saving would replace the server's note without it ever being shown. */
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

    // The page reads the note, as cook mode does when it opens.
    renderWithProviders(PersonalNotePanel, { props: { recipeId: 'r1', variant: 'cook' } });
    void notes.load('r1');

    const alert = await screen.findByRole('alert');

    expect(screen.queryByRole('textbox')).toBeNull();

    await userEvent.click(within(alert).getByRole('button'));

    await waitFor(() => expect(screen.getByRole('textbox')).toHaveValue('Use the heavy pan'));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

/* Leaving sends what was typed, not what was not: the note on screen may never have been read, and saving would replace the real one. */
describe('leaving a note nobody typed in', () => {
  /** Answers every request alike, and returns the methods it was sent. */
  const answered = (response: () => Response) => {
    const sent: string[] = [];

    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: Request) => {
        sent.push(input.method);

        return response();
      })
    );

    return sent;
  };

  it('sends nothing once it has been read', async () => {
    const sent = answered(
      () =>
        new Response(JSON.stringify({ overall: 'Use the heavy pan', steps: [] }), {
          headers: { 'Content-Type': 'application/json' }
        })
    );

    const { unmount } = renderWithProviders(PersonalNotePanel, {
      props: { recipeId: 'r1', variant: 'cook' }
    });
    void notes.load('r1');

    await waitFor(() => expect(screen.getByRole('textbox')).toHaveValue('Use the heavy pan'));
    unmount();
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(sent).toEqual(['GET']);
  });

  it('sends nothing when it could not be read', async () => {
    const sent = answered(() => new Response(null, { status: 503 }));

    const { unmount } = renderWithProviders(PersonalNotePanel, {
      props: { recipeId: 'r1', variant: 'cook' }
    });
    void notes.load('r1');

    await screen.findByRole('alert');
    unmount();
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(sent).toEqual(['GET']);
  });
});

describe('a note on the step being cooked', () => {
  it('is written apart from the recipe note, under its own label', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({ overall: null, steps: [{ stepId: 's2', body: 'Lower the heat' }] }),
            {
              headers: { 'Content-Type': 'application/json' }
            }
          )
      )
    );

    renderWithProviders(PersonalNotePanel, {
      props: { recipeId: 'r1', variant: 'cook', step: { id: 's2', name: 'Step 2' } }
    });
    void notes.load('r1');

    const stepNote = await screen.findByRole('textbox', { name: 'Only for Step 2' });

    await waitFor(() => expect(stepNote).toHaveValue('Lower the heat'));

    await userEvent.type(stepNote, '!');

    expect(notes.stepNote('s2')).toBe('Lower the heat!');
    expect(notes.overall).toBe('');
  });
});
