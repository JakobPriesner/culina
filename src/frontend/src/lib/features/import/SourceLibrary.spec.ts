import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import SourceLibrary from './SourceLibrary.svelte';
import { sources } from './stores/sources.svelte';
import type { ConnectedSource } from './types';
import { renderWithProviders } from '$lib/test/render';

/*
 * The screen where somebody decides what to keep. What it has to get right is
 * the difference between "I already have this" and "this did not come through",
 * because that is the question on every visit after the first.
 */
const source: ConnectedSource = {
  sourceId: 's1',
  kind: 'tandoor',
  label: 'recipes.example.com',
  address: 'https://recipes.example.com',
  createdAt: '2026-09-17T00:00:00Z',
  lastUsedAt: null
};

const theirs = (externalId: string, title: string, alreadyHere: string | null = null) => ({
  externalId,
  title,
  description: null,
  imageUrl: null,
  totalMinutes: 30,
  alreadyHere
});

function libraryHolds(items: ReturnType<typeof theirs>[], total = items.length) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ items, nextPage: null, total }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    )
  );
}

beforeEach(() => {
  sources.reset();
});

describe('looking through somebody else’s library', () => {
  it('offers what is not here and marks what already is', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen'), theirs('2', 'Linsensuppe', 'r9')]);

    await sources.browse(source);

    renderWithProviders(SourceLibrary, { props: { source, onimport: () => {} } });

    // Shown rather than hidden: dropping it would leave somebody unable to tell
    // "I have it" from "it did not come through".
    expect(screen.getByRole('checkbox', { name: 'Zwiebelkuchen' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: 'Linsensuppe' })).not.toBeInTheDocument();
    expect(screen.getByText('Linsensuppe')).toBeInTheDocument();
    expect(screen.getByText('Already here')).toBeInTheDocument();
  });

  it('says how many they have over there', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen')], 412);

    await sources.browse(source);

    renderWithProviders(SourceLibrary, { props: { source, onimport: () => {} } });

    expect(screen.getByText('412 recipes there')).toBeInTheDocument();
  });

  it('hands back exactly what was ticked', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen'), theirs('2', 'Linsensuppe')]);

    await sources.browse(source);

    const chosen = vi.fn();

    renderWithProviders(SourceLibrary, { props: { source, onimport: chosen } });

    await userEvent.click(screen.getByRole('checkbox', { name: 'Zwiebelkuchen' }));
    await userEvent.click(screen.getByRole('button', { name: 'Bring these over' }));

    expect(chosen).toHaveBeenCalledWith(['1']);
  });

  it('selects everything loaded, and never what has not been read', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen'), theirs('2', 'Linsensuppe', 'r9')], 2000);

    await sources.browse(source);

    const chosen = vi.fn();

    renderWithProviders(SourceLibrary, { props: { source, onimport: chosen } });

    await userEvent.click(screen.getByRole('checkbox', { name: 'Everything loaded' }));
    await userEvent.click(screen.getByRole('button', { name: 'Bring these over' }));

    // Two thousand over there, one that can still be brought over here. A
    // "select all" that quietly means something else is worse than none.
    expect(chosen).toHaveBeenCalledWith(['1']);
  });

  it('keeps the action out of the way until something is chosen', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen')]);

    await sources.browse(source);

    renderWithProviders(SourceLibrary, { props: { source, onimport: () => {} } });

    expect(screen.queryByRole('button', { name: 'Bring these over' })).not.toBeInTheDocument();
  });
});
