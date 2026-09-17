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

/** A library that arrives a page at a time, like a real one. */
function libraryHoldsPages(pages: ReturnType<typeof theirs>[][], total: number) {
  let asked = 0;

  const fetched = vi.fn(() => {
    const items = pages[asked] ?? [];
    const nextPage = asked < pages.length - 1 ? `page-${asked + 1}` : null;

    asked += 1;

    return Promise.resolve(
      new Response(JSON.stringify({ items, nextPage, total }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    );
  });

  vi.stubGlobal('fetch', fetched);

  return fetched;
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

  it('fetches the rest of the library before selecting all of it', async () => {
    const fetched = libraryHoldsPages(
      [
        [theirs('1', 'Zwiebelkuchen'), theirs('2', 'Linsensuppe')],
        [theirs('3', 'Gulasch'), theirs('4', 'Rouladen', 'r9')],
        [theirs('5', 'Knödel')]
      ],
      5
    );

    await sources.browse(source);

    const chosen = vi.fn();

    renderWithProviders(SourceLibrary, { props: { source, onimport: chosen } });

    await userEvent.click(screen.getByRole('checkbox', { name: 'All' }));
    await userEvent.click(screen.getByRole('button', { name: 'Bring these over' }));

    // Three reads: the first page, then the two it went and got. "All" that
    // meant "the page you can see" is the lie this control exists to avoid.
    expect(fetched).toHaveBeenCalledTimes(3);

    // Everything except the one already here.
    expect(chosen).toHaveBeenCalledWith(['1', '2', '3', '5']);
  });

  it('does not claim everything is chosen while there is more to read', async () => {
    libraryHoldsPages([[theirs('1', 'Zwiebelkuchen')], [theirs('2', 'Linsensuppe')]], 2);

    await sources.browse(source);

    renderWithProviders(SourceLibrary, { props: { source, onimport: () => {} } });

    await userEvent.click(screen.getByRole('checkbox', { name: 'Zwiebelkuchen' }));

    // Every loaded recipe is ticked, and the box still must not say "all".
    expect(screen.getByRole('checkbox', { name: 'All' })).not.toBeChecked();
  });

  it('stops asking when a page fails, and selects what did arrive', async () => {
    let asked = 0;

    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        asked += 1;

        if (asked === 1) {
          return Promise.resolve(
            new Response(
              JSON.stringify({ items: [theirs('1', 'Zwiebelkuchen')], nextPage: 'p1', total: 9 }),
              { status: 200, headers: { 'Content-Type': 'application/json' } }
            )
          );
        }

        return Promise.resolve(
          new Response(JSON.stringify({ code: 'import.could_not_fetch', detail: 'Nope' }), {
            status: 400,
            headers: { 'Content-Type': 'application/json' }
          })
        );
      })
    );

    await sources.browse(source);

    const chosen = vi.fn();

    renderWithProviders(SourceLibrary, { props: { source, onimport: chosen } });

    await userEvent.click(screen.getByRole('checkbox', { name: 'All' }));

    // Exactly two: the page that worked and the one that did not. A failure
    // leaves the page token where it was, so without the guard this would ask
    // the identical question until the ceiling stopped it.
    expect(asked).toBe(2);

    // A half-read library is better than none, and the failure is on screen.
    await userEvent.click(screen.getByRole('button', { name: 'Bring these over' }));

    expect(chosen).toHaveBeenCalledWith(['1']);
  });

  it('keeps the action out of the way until something is chosen', async () => {
    libraryHolds([theirs('1', 'Zwiebelkuchen')]);

    await sources.browse(source);

    renderWithProviders(SourceLibrary, { props: { source, onimport: () => {} } });

    expect(screen.queryByRole('button', { name: 'Bring these over' })).not.toBeInTheDocument();
  });
});
