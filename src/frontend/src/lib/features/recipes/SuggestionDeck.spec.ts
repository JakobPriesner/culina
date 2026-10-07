import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import SuggestionDeck from './SuggestionDeck.svelte';
import type { Suggestion } from './types';
import { renderWithProviders } from '$lib/test/render';

/* Pins two things: every suggestion is really in the document (a scroll-snap track, so swipe, keyboard and readers get the content free),
 * and the position is read off the scroll rather than remembered from a button press. */
const suggestion = (id: string, title: string): Suggestion => ({
  id,
  title,
  imageId: null,
  totalMinutes: 25,
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
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

/** A track with a width (jsdom lays nothing out); `scrollTo` moves `scrollLeft` and fires scroll as a finger would. */
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

    // A thumb rather than the control: the deck must still know where it is.
    track.scrollLeft = 1600;
    track.dispatchEvent(new Event('scroll'));
    await Promise.resolve();

    expect(screen.getByText('3 of 3')).toBeInTheDocument();
  });

  it('writes nothing down when it moves', async () => {
    // Moving on is not feedback: the ranking deliberately has no non-click signal, and a swipe meaning "never again" would collide with Dismiss.
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

  it('does not print the same reason on two panels in a row', () => {
    // Five identical "keep coming back to" reasons are one fact, so the copies wear the fixed line.
    const favourites = shortlist.map((one) => ({
      ...one,
      reason: { code: 'affinity' as const, subject: null }
    }));

    renderWithProviders(SuggestionDeck, { props: { items: favourites } });

    const eyebrows = [...document.querySelectorAll('.eyebrow')].map((line) => line.textContent);

    expect(new Set(eyebrows).size).toBe(2);
    expect(eyebrows[1]).toBe(eyebrows[2]);
    expect(eyebrows[0]).not.toBe(eyebrows[1]);
  });

  it('is the page it always was when there is only one answer', () => {
    // One suggestion, or none with the old photograph, gets no controls and nothing that scrolls.
    renderWithProviders(SuggestionDeck, { props: { items: [shortlist[0]] } });

    expect(screen.queryByRole('button', { name: 'Next suggestion' })).not.toBeInTheDocument();
    expect(screen.queryByText('1 of 1')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Linsensuppe' })).toBeInTheDocument();
  });

  it('asks for more once the last one is on screen, and not before', async () => {
    const onmore = vi.fn();

    renderWithProviders(SuggestionDeck, { props: { items: shortlist, onmore } });
    layOut();

    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));

    expect(onmore).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Next suggestion' }));

    expect(onmore).toHaveBeenCalled();
  });

  it('does not ask for more of a single answer nobody has walked', () => {
    const onmore = vi.fn();

    renderWithProviders(SuggestionDeck, { props: { items: [shortlist[0]], onmore } });

    expect(onmore).not.toHaveBeenCalled();
  });
});
