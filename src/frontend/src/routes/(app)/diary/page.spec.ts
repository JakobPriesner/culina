import { screen, waitFor, within } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { session } from '$features/auth/session.svelte';
import { diary } from '$features/diary/stores/diary.svelte';
import { diarySettings } from '$features/diary/stores/diarySettings.svelte';
import { renderWithProviders } from '$lib/test/render';

import DiaryPage from './+page.svelte';

/* The "Eintragen" journey on the page: the search, an amount for the food chosen, and the way back to the search. */
const json = (body: unknown) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

const settings = {
  enabled: true,
  currentConsentVersion: 1,
  consentVersion: 1,
  consentedAt: '2026-10-01T08:00:00Z',
  kcalTarget: 2000,
  proteinTarget: 100,
  headline: 'eaten',
  version: 1
};

const foods = [
  { code: 'B1', nameDe: 'Haferflocken', energyKcal: 372, units: [] },
  { code: 'B2', nameDe: 'Haferdrink', energyKcal: 45, units: [] }
];

function serve() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: Request) => {
      const url = new URL(input.url);

      if (url.pathname.endsWith('/settings/diary')) return json(settings);

      if (url.pathname.endsWith('/users/me/diary')) {
        const date = url.searchParams.get('from')!;

        return json({
          days: [
            {
              date,
              entries: [],
              totals: { kcal: null, protein: null, carbohydrate: null, fat: null },
              atLeast: false,
              estimated: false,
              typedKcal: 0
            }
          ]
        });
      }

      if (url.pathname.endsWith('/api/v1/foods')) return json({ items: foods });

      return json({ items: [], from: '2026-10-05', days: [] });
    })
  );
}

beforeEach(() => {
  diary.reset();
  diarySettings.reset();
  localStorage.clear();
  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue('h1');

  // jsdom has no top layer, so showModal/close are stubbed to the open state and close event.
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'getAnimations');
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('logging a food from the search', () => {
  async function toAmount() {
    serve();
    await diarySettings.load();
    renderWithProviders(DiaryPage);

    await userEvent.click(await screen.findByRole('button', { name: /Log|Eintragen/ }));

    const search = await screen.findByRole('searchbox');

    await userEvent.type(search, 'Hafer');
    await userEvent.click(await screen.findByRole('button', { name: /Haferflocken/ }));

    return search;
  }

  it('goes back to the search with what was typed and found, and leaves with Close', async () => {
    const search = await toAmount();

    const amount = await screen.findByRole('dialog', { name: 'Haferflocken' });

    expect(search).toHaveValue('Hafer');

    await userEvent.click(within(amount).getByRole('button', { name: 'Back' }));

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Haferflocken' })).toBeNull());
    expect(screen.getByRole('searchbox')).toBe(search);
    expect(screen.getByRole('searchbox')).toHaveValue('Hafer');
    expect(screen.getByRole('button', { name: /Haferflocken/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Haferdrink/ })).toBeInTheDocument();

    // Another food from the same results, then Close: both sheets go.
    await userEvent.click(screen.getByRole('button', { name: /Haferdrink/ }));

    const second = await screen.findByRole('dialog', { name: 'Haferdrink' });

    await userEvent.click(within(second).getByRole('button', { name: 'Dismiss' }));

    await waitFor(() => expect(screen.queryByRole('searchbox')).toBeNull());
  });
});
