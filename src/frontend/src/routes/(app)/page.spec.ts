import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import LibraryPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { libraryView } from '$features/recipes/stores/libraryView.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { savedSearches } from '$features/recipes/stores/savedSearches.svelte';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import { renderWithProviders } from '$lib/test/render';
import { toaster } from '$shell/toaster.svelte';

/* The library rendered with a real `$effect`: catches a store re-triggering its own effect (429 loop) and an order that changes silently. */
const household = 'h1';

const summary = (id: string, title: string) => ({
  recipeId: id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  ingredientMatch: null
});

const suggestion = (
  id: string,
  title: string,
  reason: { code: string; subject: string | null } | null
) => ({ ...summary(id, title), reason });

const json = (body: unknown) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });

function serverAnswers(reasoned: { code: string; subject: string | null } | null) {
  const fetched = vi.fn((input: Request) => Promise.resolve(answer(input.url, reasoned)));

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

/** Saved searches get their own shape: falling through to the recipe list gave that store rows without criteria and crashed after the test passed. */
function answer(url: string, reasoned: { code: string; subject: string | null } | null) {
  if (url.includes('/suggestions')) {
    return json({ items: [suggestion('r1', 'Linsensuppe', reasoned)] });
  }

  if (url.includes('/searches')) {
    return json({ items: [] });
  }

  return json({
    items: [summary('r1', 'Linsensuppe'), summary('r2', 'Omelette')],
    nextCursor: null,
    total: 2
  });
}

const settle = async () => {
  for (let turn = 0; turn < 6; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  recipes.reset();
  suggestions.reset();
  libraryView.reset();
  savedSearches.reset();
  // jsdom keeps storage for the whole file; clear it so each test opens on a device never told the ranking.
  localStorage.clear();

  for (const toast of [...toaster.toasts]) {
    toaster.dismiss(toast.id);
  }

  vi.spyOn(session, 'activeHouseholdId', 'get').mockReturnValue(household);
});

describe('opening the library', () => {
  it('asks each of its three questions exactly once', async () => {
    const fetched = serverAnswers({ code: 'rediscovery', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    // Exactly three calls (list, shortlist, saved searches): every extra one is an effect re-triggering itself.
    const asked = (part: string) => fetched.mock.calls.filter(([r]) => r.url.includes(part)).length;

    expect(fetched).toHaveBeenCalledTimes(3);
    expect(asked('/suggestions')).toBe(1);
    expect(asked('/searches')).toBe(1);
    expect(asked('/recipes?')).toBe(1);
  });

  it('leads with the suggestion, and says why', async () => {
    serverAnswers({ code: 'ingredient', subject: 'Aubergine' });

    renderWithProviders(LibraryPage);
    await settle();

    expect(await screen.findByText(/Aubergine/)).toBeInTheDocument();
  });

  it('names the order it is in', async () => {
    serverAnswers({ code: 'rediscovery', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    // Scoped to the note: the filter panel ticks the same words.
    expect(
      screen.getByText('Recently updated', { selector: '.collection-note' })
    ).toBeInTheDocument();
  });

  it('keeps the old order, and says so, before the ranking has anything to say', async () => {
    // No history means no reason: one appears only when a score term actually dominates.
    serverAnswers(null);

    renderWithProviders(LibraryPage);
    await settle();

    expect(
      screen.getByText('Recently updated', { selector: '.collection-note' })
    ).toBeInTheDocument();

    expect(screen.queryByText('For tonight')).not.toBeInTheDocument();
  });

  it('offers no order control until there is a second order worth having', async () => {
    serverAnswers(null);

    renderWithProviders(LibraryPage);
    await settle();

    expect(screen.queryByText('For tonight')).not.toBeInTheDocument();
  });

  it('hides a suggestion when told to, and offers the undo', async () => {
    const fetched = serverAnswers({ code: 'affinity', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    await userEvent.click(screen.getByRole('button', { name: /Linsensuppe/ }));
    await settle();

    const dismissals = fetched.mock.calls
      .map(([request]) => request)
      .filter((request) => request.url.includes('suggestion-dismissal'));

    expect(dismissals).toHaveLength(1);
    expect(dismissals[0]?.method).toBe('PUT');

    // Asserted on the toaster: the toast outlet lives in the app shell, absent from a page rendered alone.
    const toast = toaster.toasts.at(-1);

    expect(toast?.action?.label()).toBe('Undo');

    toast?.action?.run();
    await settle();

    expect(
      fetched.mock.calls
        .map(([request]) => request)
        .filter((request) => request.url.includes('suggestion-dismissal'))
        .map((request) => request.method)
    ).toEqual(['PUT', 'DELETE']);
  });

  it('lists all recipes in the grid below the suggestion deck', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) => {
        if (input.url.includes('/suggestions')) {
          return Promise.resolve(
            json({
              items: [
                suggestion('r1', 'Linsensuppe', { code: 'affinity', subject: null }),
                suggestion('r2', 'Omelette', { code: 'rediscovery', subject: null })
              ]
            })
          );
        }

        if (input.url.includes('/searches')) {
          return Promise.resolve(json({ items: [] }));
        }

        return Promise.resolve(
          json({
            items: [
              summary('r1', 'Linsensuppe'),
              summary('r2', 'Omelette'),
              summary('r3', 'Ratatouille')
            ],
            nextCursor: null,
            total: 3
          })
        );
      })
    );

    renderWithProviders(LibraryPage);
    await settle();

    expect(screen.getByRole('heading', { name: 'Linsensuppe' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Omelette' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Linsensuppe/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Omelette/ })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ratatouille/ })).toBeInTheDocument();
  });

  it('asks the server for the recent order by default', async () => {
    const fetched = serverAnswers({ code: 'affinity', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    const listed = fetched.mock.calls
      .map(([request]) => request.url)
      .filter((url) => url.includes('/recipes'));

    expect(listed.some((url) => url.includes('sort=-updatedAt'))).toBe(true);
  });

  it('asks the server for the suggested order when explicitly chosen', async () => {
    libraryView.forHousehold(household);
    libraryView.sort = 'suggested';
    const fetched = serverAnswers({ code: 'affinity', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    const listed = fetched.mock.calls
      .map(([request]) => request.url)
      .filter((url) => url.includes('/recipes'));

    expect(listed.some((url) => url.includes('sort=suggested'))).toBe(true);
  });
});

/* The shortlist does not decide the order, so the list is requested alongside it. */
describe('waiting for the shortlist', () => {
  function shortlistHeldBack() {
    const fetched = vi.fn((input: Request) =>
      input.url.includes('/suggestions')
        ? new Promise<Response>(() => {})
        : Promise.resolve(answer(input.url, null))
    );

    vi.stubGlobal('fetch', fetched);

    return () =>
      fetched.mock.calls.map(([request]) => request.url).filter((url) => url.includes('/recipes?'));
  }

  it('asks for the list alongside the shortlist immediately', async () => {
    const listed = shortlistHeldBack();

    renderWithProviders(LibraryPage);
    await settle();

    expect(listed()).toHaveLength(1);
    expect(listed()[0]).toContain('sort=-updatedAt');
  });

  it('holds the grid back until the shortlist is in, even with the list back', async () => {
    localStorage.setItem(`culina.ranks.${household}`, 'yes');
    shortlistHeldBack();

    renderWithProviders(LibraryPage);
    await settle();

    // Drawing the grid first would move every card when the shortlist lands.
    expect(screen.getByRole('status', { name: 'Loading your recipes' })).toBeInTheDocument();
    expect(screen.queryByText('Omelette')).not.toBeInTheDocument();
  });

  it('holds the panel back until the list is in, even with the shortlist back', async () => {
    localStorage.setItem(`culina.ranks.${household}`, 'yes');
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        input.url.includes('/recipes?')
          ? new Promise<Response>(() => {})
          : Promise.resolve(answer(input.url, { code: 'rediscovery', subject: null }))
      )
    );

    renderWithProviders(LibraryPage);
    await settle();

    // Drawing it over the skeleton would push the skeleton down; CI measures that as a page shift.
    expect(screen.getByRole('status', { name: 'Loading your recipes' })).toBeInTheDocument();
    expect(screen.queryByText('Linsensuppe')).not.toBeInTheDocument();
  });

  it('does not wait when somebody has chosen the order', async () => {
    libraryView.forHousehold(household);
    libraryView.sort = 'title';
    const listed = shortlistHeldBack();

    renderWithProviders(LibraryPage);
    await settle();

    expect(listed()).toHaveLength(1);
    expect(listed()[0]).toContain('sort=title');
  });

  it('keeps the order it began with when the fresh answer disagrees', async () => {
    localStorage.setItem(`culina.ranks.${household}`, 'no');
    const fetched = serverAnswers({ code: 'affinity', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    const listed = fetched.mock.calls
      .map(([request]) => request.url)
      .filter((url) => url.includes('/recipes?'));

    // Re-listing in the new order is the rearranging this guards against.
    expect(listed).toHaveLength(1);
    expect(listed[0]).toContain('sort=-updatedAt');
    expect(
      screen.getByText('Recently updated', { selector: '.collection-note' })
    ).toBeInTheDocument();

    expect(localStorage.getItem(`culina.ranks.${household}`)).toBe('yes');
  });

  it('remembers nothing from a shortlist that failed', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        Promise.resolve(
          input.url.includes('/suggestions')
            ? new Response(null, { status: 503 })
            : answer(input.url, null)
        )
      )
    );

    renderWithProviders(LibraryPage);
    await settle();

    // A failure must not be remembered as "nothing to say", or a ranked kitchen stays in recent order.
    expect(localStorage.getItem(`culina.ranks.${household}`)).toBeNull();
  });
});

describe('keyboard shortcuts', () => {
  it('focuses and selects the search input on Cmd+F / Ctrl+F', async () => {
    serverAnswers(null);
    renderWithProviders(LibraryPage);
    await settle();

    const searchField = screen.getByRole('searchbox', { name: 'Search recipes' });
    expect(searchField).not.toHaveFocus();

    const event = new KeyboardEvent('keydown', {
      key: 'f',
      metaKey: true,
      bubbles: true,
      cancelable: true
    });
    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(searchField).toHaveFocus();
  });

  it('works with Ctrl+F as well', async () => {
    serverAnswers(null);
    renderWithProviders(LibraryPage);
    await settle();

    const searchField = screen.getByRole('searchbox', { name: 'Search recipes' });
    const event = new KeyboardEvent('keydown', {
      key: 'f',
      ctrlKey: true,
      bubbles: true,
      cancelable: true
    });
    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(searchField).toHaveFocus();
  });

  it('selects existing text in the search field on Cmd+F', async () => {
    serverAnswers(null);
    renderWithProviders(LibraryPage);
    await settle();

    const searchField = screen.getByRole<HTMLInputElement>('searchbox', { name: 'Search recipes' });
    await userEvent.type(searchField, 'soup');
    expect(searchField).toHaveValue('soup');

    searchField.blur();
    expect(searchField).not.toHaveFocus();

    const event = new KeyboardEvent('keydown', {
      key: 'f',
      metaKey: true,
      bubbles: true,
      cancelable: true
    });
    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(searchField).toHaveFocus();
    expect(searchField.selectionStart).toBe(0);
    expect(searchField.selectionEnd).toBe(4);
  });

  it('does not steal focus when a modal dialog is open', async () => {
    serverAnswers(null);
    renderWithProviders(LibraryPage);
    await settle();

    const dialog = document.createElement('dialog');
    dialog.open = true;
    document.body.appendChild(dialog);

    try {
      const searchField = screen.getByRole('searchbox', { name: 'Search recipes' });
      searchField.blur();

      const event = new KeyboardEvent('keydown', {
        key: 'f',
        metaKey: true,
        bubbles: true,
        cancelable: true
      });
      window.dispatchEvent(event);

      expect(event.defaultPrevented).toBe(false);
      expect(searchField).not.toHaveFocus();
    } finally {
      document.body.removeChild(dialog);
    }
  });
});
