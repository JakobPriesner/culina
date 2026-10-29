import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import ScanSheet from './ScanSheet.svelte';
import { renderWithProviders } from '#lib/test/render.js';
import { preferences } from '$shell/preferences.svelte';

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' }
  });

const barilla = {
  gtin: '8076800195057',
  name: 'Barilla Spaghetti n.5',
  ingredient: 'Spaghetti',
  cookMinutes: 9,
  householdId: 'h1',
  version: 1
};

let sent: { method: string; url: string }[];

/** The server as a household that knows Barilla and nothing else. */
function serve(answerPut?: () => Response) {
  sent = [];
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      sent.push({ method: input.method, url: input.url });

      if (input.method === 'PUT') {
        return answerPut
          ? answerPut()
          : json({ ...barilla, gtin: '4008400402222', name: 'Pesto' }, 201);
      }

      return input.url.endsWith('/8076800195057')
        ? json(barilla)
        : json({ code: 'products.unknown', detail: 'unknown', status: 404 }, 404);
    })
  );
}

function show(extra: Record<string, unknown> = {}) {
  return renderWithProviders(ScanSheet, {
    props: { open: true, householdId: 'h1', ...extra }
  });
}

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  preferences.setLocale('en');
});

afterEach(() => vi.unstubAllGlobals());

describe('the scan sheet, without a camera decoder', () => {
  it('says the camera cannot read barcodes here and offers the digits, never a dead end', async () => {
    serve();
    show();

    expect(await screen.findByText(/cannot read barcodes from the camera/)).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Barcode digits' })).toBeInTheDocument();
  });

  it('tells a mistyped number on the spot and sends nothing', async () => {
    serve();
    show();

    await userEvent.type(screen.getByRole('textbox', { name: 'Barcode digits' }), '8076800195058');
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));

    expect(screen.getByText(/not a valid barcode number/)).toBeInTheDocument();
    expect(sent).toEqual([]);
  });

  it('shows what the household calls a barcode it knows, with its cooking time and the page’s buttons', async () => {
    serve();
    const onresolved = vi.fn();

    show({ onresolved });

    await userEvent.type(
      screen.getByRole('textbox', { name: 'Barcode digits' }),
      '8076 800195 057'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));

    expect(
      await screen.findByRole('heading', { name: 'Barilla Spaghetti n.5' })
    ).toBeInTheDocument();
    expect(screen.getByText('Counts as Spaghetti')).toBeInTheDocument();
    expect(screen.getByText('9 min, as your household noted')).toBeInTheDocument();
    expect(onresolved).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Barilla Spaghetti n.5' })
    );
    expect(sent).toEqual([
      { method: 'GET', url: expect.stringMatching(/\/products\/8076800195057$/) }
    ]);
  });

  it('asks for a name once when nobody named the barcode, and keeps the answer in the household’s memory', async () => {
    serve();
    show();

    await userEvent.type(screen.getByRole('textbox', { name: 'Barcode digits' }), '4008400402222');
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));

    expect(await screen.findByRole('heading', { name: 'Not known here yet' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Save the name' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Give it a name first.');

    await userEvent.type(screen.getByRole('textbox', { name: 'What is it called?' }), 'Pesto');
    await userEvent.click(screen.getByRole('button', { name: 'Save the name' }));

    expect(await screen.findByRole('heading', { name: 'Pesto' })).toBeInTheDocument();
    expect(sent.map((one) => one.method)).toEqual(['GET', 'PUT']);
  });

  it('shows what another phone named meanwhile instead of failing, when it was named first', async () => {
    let named = false;

    serve(() => {
      named = true;

      return json({ code: 'products.already_named', detail: 'named', status: 428 }, 428);
    });

    // The first look finds nothing; after the refused naming the code is known.
    const answer = globalThis.fetch;

    vi.stubGlobal('fetch', async (input: Request) =>
      named && input.method === 'GET'
        ? json({ ...barilla, name: 'Pesto', gtin: '4008400402222' })
        : answer(input)
    );
    show();

    await userEvent.type(screen.getByRole('textbox', { name: 'Barcode digits' }), '4008400402222');
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));
    await userEvent.type(
      await screen.findByRole('textbox', { name: 'What is it called?' }),
      'Pasta'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Save the name' }));

    await waitFor(() => expect(screen.getByRole('heading', { name: 'Pesto' })).toBeInTheDocument());
  });

  it('says why a lookup failed, in words, and lets it be tried again', async () => {
    vi.stubGlobal('fetch', async () =>
      json({ code: 'client.offline', detail: 'x', status: 503 }, 503)
    );
    show();

    await userEvent.type(screen.getByRole('textbox', { name: 'Barcode digits' }), '8076800195057');
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('goes back to scanning for the next one', async () => {
    serve();
    show();

    await userEvent.type(screen.getByRole('textbox', { name: 'Barcode digits' }), '8076800195057');
    await userEvent.click(screen.getByRole('button', { name: 'Look it up' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Scan another' }));

    expect(screen.getByRole('textbox', { name: 'Barcode digits' })).toHaveValue('');
  });
});
