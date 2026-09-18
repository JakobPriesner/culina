import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import LibraryPage from './+page.svelte';
import { session } from '$features/auth/session.svelte';
import { libraryView } from '$features/recipes/stores/libraryView.svelte';
import { recipes } from '$features/recipes/stores/recipes.svelte';
import { suggestions } from '$features/recipes/stores/suggestions.svelte';
import { renderWithProviders } from '$lib/test/render';
import { toaster } from '$shell/toaster.svelte';

/*
 * The library, rendered, because two of the things this page can now get wrong
 * only exist once a real `$effect` is running.
 *
 * The first is the loop: a store that guarded itself with `$state` would make
 * the effect that called it re-trigger itself, and the page would ask the same
 * question until the session rate limiter started answering 429. A store test
 * cannot see that — nothing about the call in isolation is wrong, it is the
 * pairing with the effect that is.
 *
 * The second is the order the page claims to be in. A list whose order changed
 * without saying so is the thing that makes people stop trusting an app.
 */
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

/** One server, answering both of the page's questions by URL. */
function serverAnswers(reasoned: { code: string; subject: string | null } | null) {
  const fetched = vi.fn((input: Request) =>
    Promise.resolve(
      input.url.includes('/suggestions')
        ? json({ items: [suggestion('r1', 'Linsensuppe', reasoned)] })
        : json({
            items: [summary('r1', 'Linsensuppe'), summary('r2', 'Omelette')],
            nextCursor: null,
            total: 2
          })
    )
  );

  vi.stubGlobal('fetch', fetched);

  return fetched;
}

/** Lets every queued effect and the request it made settle. */
const settle = async () => {
  for (let turn = 0; turn < 6; turn += 1) {
    await new Promise((resume) => setTimeout(resume, 0));
  }
};

beforeEach(() => {
  recipes.reset();
  suggestions.reset();
  libraryView.reset();

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

    // Three, and one of each. Not "a reasonable number": every extra call is an
    // effect that re-triggered itself, and the next one after that is the rate
    // limiter. The third is the saved searches, which the toolbar draws as
    // chips and so cannot wait until something is opened — unlike the tag
    // vocabulary, which is only read when the filter panel is.
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

    // The panel was always here. What changed is that the recipe in it is an
    // answer rather than the first one that happened to have a photograph — and
    // the eyebrow is what makes it read as one.
    expect(await screen.findByText(/Aubergine/)).toBeInTheDocument();
  });

  it('names the order it is in', async () => {
    serverAnswers({ code: 'rediscovery', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    // Scoped to the note, because the filter panel ticks the very same words —
    // which is the point: the line above the grid and the control that sets it
    // cannot describe the list differently.
    expect(screen.getByText('For tonight', { selector: '.collection-note' })).toBeInTheDocument();
  });

  it('keeps the old order, and says so, before the ranking has anything to say', async () => {
    // A kitchen with no history gets the app it has always had. Nothing guesses
    // a threshold: a reason exists exactly when one term of the score actually
    // dominated, which is the same thing as "there is enough history here".
    serverAnswers(null);

    renderWithProviders(LibraryPage);
    await settle();

    expect(
      screen.getByText('Recently updated', { selector: '.collection-note' })
    ).toBeInTheDocument();

    // And the ranking is not even offered as an order yet.
    expect(screen.queryByText('For tonight')).not.toBeInTheDocument();
  });

  it('offers no order control until there is a second order worth having', async () => {
    serverAnswers(null);

    renderWithProviders(LibraryPage);
    await settle();

    expect(screen.queryByText('For tonight')).not.toBeInTheDocument();
  });

  it('hides a suggestion when told to, and offers the undo', async () => {
    // The only negative signal the ranking cannot derive from something another
    // feature already records, so it has to be sayable — and reversible, since
    // "not tonight" is a small decision and a confirmation dialog would make it
    // feel like a large one.
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

    // Asserted on the toaster rather than on the screen: the toast outlet lives
    // in the app shell, so a page rendered on its own has nowhere to draw one.
    // What matters here is that the page asked for an undoable message.
    const toast = toaster.toasts.at(-1);

    expect(toast?.action?.label).toBe('Undo');

    // And that the undo actually reaches the server.
    toast?.action?.run();
    await settle();

    expect(
      fetched.mock.calls
        .map(([request]) => request)
        .filter((request) => request.url.includes('suggestion-dismissal'))
        .map((request) => request.method)
    ).toEqual(['PUT', 'DELETE']);
  });

  it('keeps the grid still while the panel is walked', async () => {
    // The panel used to hold one recipe that could not change, so hiding that
    // one from the grid below was enough. A shortlist that is walked is not:
    // hiding only the visible panel would push one recipe into the grid and
    // pull another out of it on every swipe, and the page would rearrange
    // itself under the thumb that was only looking at the next idea.
    vi.stubGlobal(
      'fetch',
      vi.fn((input: Request) =>
        Promise.resolve(
          input.url.includes('/suggestions')
            ? json({
                items: [
                  suggestion('r1', 'Linsensuppe', { code: 'affinity', subject: null }),
                  suggestion('r2', 'Omelette', { code: 'rediscovery', subject: null })
                ]
              })
            : json({
                items: [
                  summary('r1', 'Linsensuppe'),
                  summary('r2', 'Omelette'),
                  summary('r3', 'Ratatouille')
                ],
                nextCursor: null,
                total: 3
              })
        )
      )
    );

    renderWithProviders(LibraryPage);
    await settle();

    // Both shortlisted recipes are in the panel, as headings, and neither is
    // also a card below it. The grid starts where the shortlist stops.
    expect(screen.getByRole('heading', { name: 'Linsensuppe' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Omelette' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Linsensuppe/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Omelette/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ratatouille/ })).toBeInTheDocument();
  });

  it('asks the server for the suggested order once it is in it', async () => {
    const fetched = serverAnswers({ code: 'affinity', subject: null });

    renderWithProviders(LibraryPage);
    await settle();

    const listed = fetched.mock.calls
      .map(([request]) => request.url)
      .filter((url) => url.includes('/recipes'));

    expect(listed.some((url) => url.includes('sort=suggested'))).toBe(true);
  });
});
