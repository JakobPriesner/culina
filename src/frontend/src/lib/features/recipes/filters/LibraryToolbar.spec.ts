import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import LibraryToolbar from './LibraryToolbar.svelte';
import { RecipeQuery } from '../stores/libraryView.svelte';
import { savedSearches } from '../stores/savedSearches.svelte';
import { tags } from '$features/cookbooks/stores/tags.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The toolbar, rendered, because the thing worth proving about it only exists
 * on screen: every filter that is on has to be removable from where it is
 * shown. A chip that displays a filter but cannot take it off is worse than no
 * chip — it names a thing you then have to go hunting for.
 */
const household = 'h1';

const json = (body: unknown) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

const saved = (id: string, name: string, criteria: Record<string, unknown>) => ({
  searchId: id,
  householdId: household,
  name,
  criteria,
  createdBy: 'u1',
  createdAt: '2026-09-18T00:00:00Z',
  updatedAt: '2026-09-18T00:00:00Z'
});

const settle = async () => {
  for (let turn = 0; turn < 4; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

function serverAnswers(searches: ReturnType<typeof saved>[] = []) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: Request) =>
      Promise.resolve(
        input.url.includes('/searches')
          ? json({ items: searches })
          : json({ items: [{ slug: 'vegetarisch', name: 'Vegetarisch', recipeCount: 4 }] })
      )
    )
  );
}

function show(view: RecipeQuery, savable = true) {
  return renderWithProviders(LibraryToolbar, {
    props: {
      id: 'search',
      householdId: household,
      view,
      context: { searching: false, ranks: false, inACookbook: false },
      searchLabel: 'Search recipes',
      searchPlaceholder: 'Recipe or ingredient',
      savable
    }
  });
}

beforeEach(() => {
  savedSearches.reset();
  tags.reset();
  vi.unstubAllGlobals();
});

describe('the library toolbar', () => {
  it('says how many filters are on, without counting the words', async () => {
    serverAnswers();

    const view = new RecipeQuery();

    view.query = 'auflauf';
    view.maxMinutes = 30;
    view.toggleTag('vegetarisch');

    show(view);
    await settle();

    // The words are already visible in the box they were typed into; a badge
    // that counted them would say "1" over an empty panel.
    expect(screen.getByRole('button', { name: /Filter \(2\)/ })).toBeInTheDocument();
  });

  it('takes a filter off from the chip that shows it', async () => {
    serverAnswers();

    const view = new RecipeQuery();

    view.maxMinutes = 30;
    show(view);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Remove the filter “Up to 30 min”' }));

    expect(view.maxMinutes).toBeNull();
  });

  it('names a tag chip by the household’s own word once it knows it', async () => {
    serverAnswers();

    // The vocabulary is read when the filter panel is opened, which a jsdom
    // dialog cannot do, so it is loaded here the way opening it would.
    await tags.load(household);

    const view = new RecipeQuery();

    view.toggleTag('vegetarisch');
    show(view);
    await settle();

    const applied = screen.getByRole('list', { name: 'Applied filters' });

    expect(within(applied).getByText('Vegetarisch')).toBeInTheDocument();
  });

  it('falls back to the slug before the vocabulary has arrived', async () => {
    // A chip that rendered nothing until a second request came back would blink
    // an empty pill onto the toolbar on every first paint.
    serverAnswers();

    const view = new RecipeQuery();

    view.toggleTag('vegetarisch');
    show(view);

    const applied = screen.getByRole('list', { name: 'Applied filters' });

    expect(within(applied).getByText('vegetarisch')).toBeInTheDocument();
  });

  it('applies every dimension of a saved search at once', async () => {
    serverAnswers([
      saved('s1', 'Schnell', { query: 'auflauf', tags: ['vegetarisch'], maxMinutes: 15 })
    ]);

    const view = new RecipeQuery();

    show(view);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: 'Schnell' }));

    expect(view.snapshot()).toEqual({
      query: 'auflauf',
      tags: ['vegetarisch'],
      maxMinutes: 15,
      sort: null
    });
  });

  it('offers no saved searches where one could not be applied again', async () => {
    // Inside a cookbook: what would be saved is the shelf's own question plus a
    // filter over it, and reapplying that from the library would find something
    // else entirely.
    serverAnswers([saved('s1', 'Schnell', { maxMinutes: 15 })]);

    show(new RecipeQuery(), false);
    await settle();

    expect(screen.queryByRole('button', { name: 'Schnell' })).not.toBeInTheDocument();
  });
});
