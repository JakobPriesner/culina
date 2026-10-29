import { fireEvent, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '#lib/test/render.js';
import { applyLocale } from '$shell/i18n';

import IdeaDraft from './IdeaDraft.svelte';
import { drafts } from './stores/drafts.svelte';

vi.mock('$features/auth/session.svelte', () => ({ session: { user: { isAdmin: true } } }));

/* A failed idea must not leave the server's English sentence or an empty progress box that outlives the request. */

const json = (body: unknown, status: number) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

/** The job the server queued, as the intake stream reports it. */
const job = (id: string, stage: string, errorCode: string | null = null) => ({
  id,
  stage,
  errorCode,
  householdId: 'h1',
  createdAt: '2026-10-14T00:00:00Z',
  purpose: 'draft',
  draftKind: 'idea',
  draft: { draftId: 'd1', groups: [], steps: [], tags: [] }
});

/** The job ending at once on a failure, with nothing written. */
const stoppedWith =
  (code: string) =>
  (id: string): Response =>
    new Response(
      `data: ${JSON.stringify({ snapshot: true, jobs: [job(id, 'failed', code)] })}\n\n`,
      {
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' }
      }
    );

/** Turned away before anything was queued, which is how an assistant switched off arrives. */
const refusedWith = (code: string, status: number) =>
  new Response(JSON.stringify({ code, detail: 'This month’s budget is spent.' }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' }
  });

/** The server: the ask queues a job (or is refused), and the stream says how the job ended. */
async function askFor(idea: string, ended: (id: string) => Response, refused?: Response) {
  let id = '';

  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request | string, init?: RequestInit) => {
      const asked =
        input instanceof Request ? input : new Request(new URL(input, location.href), init);

      if (asked.method === 'PUT') {
        id = asked.url.split('/').pop()!;

        return Promise.resolve(refused ?? json(job(id, 'queued'), 202));
      }

      return Promise.resolve(ended(id));
    })
  );

  const view = renderWithProviders(IdeaDraft, {
    props: { householdId: 'h1', language: 'de', onwritten: vi.fn(), oncancel: vi.fn() }
  });

  await fireEvent.input(screen.getByRole('textbox'), { target: { value: idea } });
  await fireEvent.click(screen.getByRole('button', { name: /Rezept generieren|Draft recipe/ }));

  await vi.waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());

  return view;
}

describe('an idea the assistant could not draft', () => {
  afterEach(() => {
    drafts.reset();
    localStorage.clear();
    vi.unstubAllGlobals();
    applyLocale('en');
  });

  it('leaves the reason in German, and no progress box behind it', async () => {
    applyLocale('de');

    const { container } = await askFor('Etwas mit Kürbis', stoppedWith('assistance.model_missing'));

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

    await askFor('Etwas mit Kürbis', stoppedWith('assistance.unusable_answer'));

    expect(screen.getByRole('textbox')).toHaveValue('Etwas mit Kürbis');
    expect(screen.getByRole('button', { name: 'Rezept generieren' })).toBeEnabled();
  });

  it('says the same in English for a request turned away before it started', async () => {
    const { container } = await askFor(
      'Something with squash',
      stoppedWith('never'),
      refusedWith('assistance.budget_exhausted', 429)
    );

    expect(screen.getByRole('alert')).toHaveTextContent("This month's AI budget is used up.");
    expect(container.querySelector('[aria-live="polite"]')).toBeNull();
  });
});
