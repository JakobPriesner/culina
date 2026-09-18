import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import SuggestionDeck from './SuggestionDeck.svelte';
import type { Suggestion } from './types';
import { renderWithProviders } from '$lib/test/render';

/*
 * The shortlist, walked.
 *
 * Two things are worth pinning here and the rest is the browser's business.
 *
 * The first is that every suggestion is really in the document. The whole
 * reason this is a scroll-snap track and not a deck of cards is that the swipe,
 * the trackpad, the keyboard and the screen reader all get the content for
 * free — but only if the content is there. A component that rendered one panel
 * and swapped it on a gesture would pass every test about buttons and still be
 * unswipeable on a phone and unreadable by a reader.
 *
 * The second is where the position comes from. It is read off the scroll, not
 * remembered from the last press, because four different things move this track
 * and only one of them is a button. A remembered index passes a test that
 * clicks Next and lies the moment a thumb is used instead.
 */
const suggestion = (id: string, title: string): Suggestion => ({
  id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  tags: [],
  cookCount: 0,
  lastCookedAt: null,
  updatedAt: '2026-09-18T00:00:00Z',
  match: null,
  reason: null
});

const shortlist = [
  suggestion('r1', 'Linsensuppe'),
  suggestion('r2', 'Omelette'),
  suggestion('r3', 'Ratatouille')
];

/**
 * A track with a width, which jsdom does not lay out.
 *
 * `scrollTo` is stubbed to move `scrollLeft` and fire the scroll the browser
 * would fire, which is exactly the path a finger takes: the component never
 * learns where it is from the thing that asked it to move.
 */
function layOut(width = 800) {
  const track = document.querySelector('ul');

  if (!track) {
    throw new Error('the deck rendered no track');
  }

  vi.spyOn(track, 'clientWidth', 'get').mockReturnValue(width);

  track.scrollTo = ((options: ScrollToOptions) => {
    track.scrollLeft = options.left ?? 0;
    track.dispatchEvent(new Event('scroll'));
  }) as typeof track.scrollTo;

  return track;
}

describe('walking the shortlist', () => {
  it('puts every suggestion in the document, so a finger has somewhere to go', () => {
    renderWithProviders(SuggestionDeck, { props: { items: shortlist } });

    for (const one of shortlist) {
      expect(screen.getByRole('heading', { name: one.title })).toBeInTheDocument();
    }
  });

  it('says which of how many', async () => {
    renderWithProviders(SuggestionDeck, { props: { items: shortlist } });
    layOut();

    expect(screen.getByText('1 of 3')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));

    expect(screen.getByText('2 of 3')).toBeInTheDocument();
  });

  it('follows the scroll, not the button', async () => {
    renderWithProviders(SuggestionDeck, { props: { items: shortlist } });
    const track = layOut();

    // A thumb, rather than the control. Nothing pressed anything, and the deck
    // still knows where it is — which is the only version of this that works on
    // the device the feature was asked for.
    track.scrollLeft = 1600;
    track.dispatchEvent(new Event('scroll'));
    await Promise.resolve();

    expect(screen.getByText('3 of 3')).toBeInTheDocument();
  });

  it('writes nothing down when it moves', async () => {
    // The load-bearing one. Moving on is not feedback: the ranking has no
    // non-click signal on purpose, because with two to eight people a recipe
    // somebody scrolled past means nothing. A gesture that quietly meant "never
    // again" would invent the one signal the backend refused to, and would
    // collide with the Dismiss control two centimetres away.
    const ondismiss = vi.fn();
    const fetched = vi.fn();

    vi.stubGlobal('fetch', fetched);

    renderWithProviders(SuggestionDeck, { props: { items: shortlist, ondismiss } });
    const track = layOut();

    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));
    track.scrollLeft = 1600;
    track.dispatchEvent(new Event('scroll'));
    await userEvent.click(screen.getByRole('button', { name: 'Previous suggestion' }));

    expect(fetched).not.toHaveBeenCalled();
    expect(ondismiss).not.toHaveBeenCalled();
  });

  it('still says "never again" when that is what was meant', async () => {
    const ondismiss = vi.fn();

    renderWithProviders(SuggestionDeck, { props: { items: shortlist, ondismiss } });

    await userEvent.click(screen.getByRole('button', { name: /Linsensuppe/ }));

    expect(ondismiss).toHaveBeenCalledWith('r1');
  });

  it('goes nowhere past either end', async () => {
    renderWithProviders(SuggestionDeck, { props: { items: shortlist } });
    layOut();

    expect(screen.getByRole('button', { name: 'Previous suggestion' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));
    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));

    expect(screen.getByRole('button', { name: 'Next suggestion' })).toBeDisabled();
    expect(screen.getByText('3 of 3')).toBeInTheDocument();
  });

  it('is the page it always was when there is only one answer', () => {
    // A kitchen with one suggestion, or with none and the old photograph in its
    // place, gets no controls and nothing that scrolls. The new thing has to
    // degrade into the old thing exactly, or it is a second design.
    renderWithProviders(SuggestionDeck, { props: { items: [shortlist[0]] } });

    expect(screen.queryByRole('button', { name: 'Next suggestion' })).not.toBeInTheDocument();
    expect(screen.queryByText('1 of 1')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Linsensuppe' })).toBeInTheDocument();
  });
});
