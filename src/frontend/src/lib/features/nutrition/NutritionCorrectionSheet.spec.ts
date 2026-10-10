import { screen, waitFor } from '@testing-library/svelte';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import NutritionCorrectionSheet from './NutritionCorrectionSheet.svelte';
import type { NutritionLine } from './types';
import { renderWithProviders } from '$lib/test/render';
import { preferences } from '$shell/preferences.svelte';

const butters = [
  { code: 'M111111', nameDe: 'Süßrahmbutter', nameEn: 'Sweet cream butter', energyKcal: 741 },
  { code: 'M222222', nameDe: 'Butterschmalz', nameEn: 'Clarified butter', energyKcal: null }
];

const counted: NutritionLine = {
  ingredientId: 'a',
  status: 'counted',
  reason: null,
  food: {
    code: 'M110100',
    nameDe: 'Butter',
    nameEn: 'Butter',
    labelDe: 'Butter',
    labelEn: 'Butter'
  },
  grams: 200,
  via: 'mass',
  corrected: false,
  canRaiseEnergy: false,
  energyKcal: 372
};

let asked: ReturnType<typeof vi.fn>;

const respond = (items: unknown[] = butters) =>
  vi.stubGlobal(
    'fetch',
    (asked = vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ items }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    ))
  );

function show(line: NutritionLine = counted) {
  const handlers = {
    onchoose: vi.fn(),
    onexclude: vi.fn(),
    onreset: vi.fn(),
    onclose: vi.fn()
  };

  renderWithProviders(NutritionCorrectionSheet, { props: { name: 'Butter', line, ...handlers } });

  return handlers;
}

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
  preferences.setLocale('en');
});

afterEach(() => {
  preferences.setLocale('en');
  vi.unstubAllGlobals();
});

describe('the correction sheet', () => {
  it('names the ingredient and says the answer holds for every recipe', async () => {
    respond();
    show();

    expect(await screen.findByRole('dialog', { name: 'What is “Butter”?' })).toBeInTheDocument();
    expect(
      screen.getByText('Applies to every recipe of yours that uses “Butter”.')
    ).toBeInTheDocument();
  });

  it('shows the current choice first with a checkmark and where it comes from', async () => {
    respond();
    show();

    const current = await screen.findByRole('button', { name: /Butter\s*Default/ });

    expect(current).toHaveAttribute('aria-current', 'true');
  });

  it('names the current food as a reader would, with the table name under it', async () => {
    respond();
    show({
      ...counted,
      food: { ...counted.food!, nameEn: 'Beef/pork, mince mixed, raw', labelEn: 'mixed mince' }
    });

    expect(
      await screen.findByRole('button', { name: /mixed mince\s*BLS: Beef\/pork, mince mixed, raw/ })
    ).toHaveAttribute('aria-current', 'true');
  });

  it('says a household choice is theirs', async () => {
    respond();
    show({ ...counted, corrected: true });

    expect(await screen.findByRole('button', { name: /Butter\s*your choice/ })).toHaveAttribute(
      'aria-current'
    );
  });

  it('searches from the name and shows each food with its energy per 100 g', async () => {
    respond();
    show();

    expect(
      await screen.findByRole('button', { name: /Sweet cream butter\s*741 kcal per 100 g/ })
    ).toBeInTheDocument();
    // A food with no energy is named without a number rather than with a made-up one.
    expect(screen.getByRole('button', { name: 'Clarified butter' })).toBeInTheDocument();
    expect(asked.mock.calls[0]?.[0].url).toContain('q=Butter');
  });

  it('chooses a food by its code', async () => {
    respond();

    const { onchoose, onclose } = show();

    await userEvent.click(await screen.findByRole('button', { name: /Sweet cream butter/ }));

    expect(onchoose).toHaveBeenCalledWith(expect.objectContaining({ code: 'M111111' }));
    expect(onclose).not.toHaveBeenCalled();
  });

  it('says plainly when nothing fits', async () => {
    respond([]);
    show();

    expect(await screen.findByText('Nothing found for “Butter”.')).toBeInTheDocument();
  });

  it('only says the search is not working when it fails, and still offers not counting', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(null, { status: 503 })))
    );

    const { onexclude } = show();

    expect(await screen.findByText(/Search is not working right now/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: "Don't count this" }));

    expect(onexclude).toHaveBeenCalled();
  });

  it('offers going back to the default only for a choice the household made', async () => {
    respond();

    const first = show();

    await screen.findByRole('button', { name: /Sweet cream butter/ });
    expect(screen.queryByRole('button', { name: 'Back to the default' })).not.toBeInTheDocument();
    expect(first.onreset).not.toHaveBeenCalled();
  });

  it('goes back to the default', async () => {
    respond();

    const { onreset } = show({ ...counted, corrected: true });

    await userEvent.click(await screen.findByRole('button', { name: 'Back to the default' }));

    expect(onreset).toHaveBeenCalled();
  });

  it('marks a household exclusion as the current choice and does not offer it again', async () => {
    respond();
    show({
      ingredientId: 'a',
      status: 'excluded',
      reason: null,
      food: null,
      grams: null,
      via: null,
      corrected: true,
      canRaiseEnergy: false,
      energyKcal: null
    });

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Don't count this/ })).toHaveAttribute(
        'aria-current'
      )
    );
    expect(screen.getAllByRole('button', { name: /Don't count this/ })).toHaveLength(1);
  });
});
