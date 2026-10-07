import { fireEvent, screen, waitFor } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import HeirsList from './HeirsList.svelte';

/* The kitchen being read is told who reads it; its owners can cut a household that inherits directly, one further down is its parent's to cut. */
const heirs = {
  items: [
    { householdId: 'flat', name: 'Flat', inheritsFrom: 'family' },
    { householdId: 'club', name: 'Club', inheritsFrom: 'flat' }
  ]
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

let send: ReturnType<typeof vi.fn>;

beforeEach(() => {
  send = vi.fn((input: Request) =>
    Promise.resolve(input.method === 'DELETE' ? new Response(null, { status: 204 }) : json(heirs))
  );
  vi.stubGlobal('fetch', send);
});

describe('the households that see these recipes', () => {
  it('names every one, and which household a further one reads through', async () => {
    renderWithProviders(HeirsList, { props: { householdId: 'family', owner: false } });

    expect(await screen.findByText('Flat')).toBeInTheDocument();
    expect(screen.getByText('through Flat')).toBeInTheDocument();
    // Told, but not able to do anything about it.
    expect(screen.queryByRole('button', { name: 'Stop sharing' })).not.toBeInTheDocument();
  });

  it('lets an owner cut loose a household inheriting directly, and only that one', async () => {
    renderWithProviders(HeirsList, { props: { householdId: 'family', owner: true } });

    const stops = await screen.findAllByRole('button', { name: 'Stop sharing' });
    expect(stops).toHaveLength(1);

    await fireEvent.click(stops[0]!);

    await waitFor(() => {
      const removed = send.mock.calls
        .map(([request]) => request as Request)
        .find((request) => request.method === 'DELETE');

      expect(removed?.url).toMatch(/\/households\/family\/heirs\/flat$/);
    });
  });

  it('says so when nobody else sees them', async () => {
    send.mockImplementation(() => Promise.resolve(json({ items: [] })));

    renderWithProviders(HeirsList, { props: { householdId: 'family', owner: true } });

    expect(await screen.findByText('No other household sees them.')).toBeInTheDocument();
  });
});
