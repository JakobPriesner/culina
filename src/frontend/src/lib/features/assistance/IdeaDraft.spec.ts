import { fireEvent, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';
import { applyLocale } from '$shell/i18n';

import IdeaDraft from './IdeaDraft.svelte';
import { drafts } from './stores/drafts.svelte';

vi.mock('$features/auth/session.svelte', () => ({ session: { user: { isAdmin: true } } }));

/*
 * What a failed idea leaves on screen. It used to leave the server's English
 * sentence and an empty, dark progress box that stayed after the request had
 * ended — so a German cook with a model nobody had pulled was told, in
 * English, that the assistant "would not answer that", above a box that looked
 * like it was still thinking.
 */

/** The assistant's stream ending at once on a failure, with nothing written. */
const stoppedWith = (code: string) =>
  new Response(
    `data: ${JSON.stringify({
      finished: true,
      draft: { draftId: 'd1', groups: [], steps: [], tags: [] },
      problem: { code, detail: 'The assistant would not answer that.' }
    })}\n\n`,
    { status: 200, headers: { 'Content-Type': 'text/event-stream' } }
  );

/** Turned away before the stream opened, which is how a spent budget arrives. */
const refusedWith = (code: string, status: number) =>
  new Response(JSON.stringify({ code, detail: 'This month’s budget is spent.' }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

async function askFor(idea: string, reply: () => Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(reply()))
  );

  const view = renderWithProviders(IdeaDraft, {
    props: { householdId: 'h1', language: 'de', onwritten: vi.fn(), oncancel: vi.fn() }
  });

  await fireEvent.input(screen.getByRole('textbox'), { target: { value: idea } });
  await fireEvent.click(screen.getByRole('button', { name: /Rezept entwerfen|Draft recipe/ }));

  await vi.waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());

  return view;
}

describe('an idea the assistant could not draft', () => {
  afterEach(() => {
    drafts.reset();
    vi.unstubAllGlobals();
    applyLocale('en');
  });

  it('leaves the reason in German, and no progress box behind it', async () => {
    applyLocale('de');

    const { container } = await askFor('Etwas mit Kürbis', () =>
      stoppedWith('assistance.model_missing')
    );

    expect(screen.getByRole('alert')).toHaveTextContent(/KI-Modell ist nicht verfügbar/);
    expect(screen.getByRole('alert')).not.toHaveTextContent('would not answer');
    expect(container.querySelector('[aria-live="polite"]')).toBeNull();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Assistent-Einstellungen öffnen' })
    ).toBeInTheDocument();
  });

  it('keeps the idea so asking again is one press', async () => {
    applyLocale('de');

    await askFor('Etwas mit Kürbis', () => stoppedWith('assistance.unusable_answer'));

    expect(screen.getByRole('textbox')).toHaveValue('Etwas mit Kürbis');
    expect(screen.getByRole('button', { name: 'Rezept entwerfen' })).toBeEnabled();
  });

  it('says the same in English for a request turned away before it started', async () => {
    const { container } = await askFor('Something with squash', () =>
      refusedWith('assistance.budget_exhausted', 429)
    );

    expect(screen.getByRole('alert')).toHaveTextContent("This month's AI budget is used up.");
    expect(container.querySelector('[aria-live="polite"]')).toBeNull();
  });
});
