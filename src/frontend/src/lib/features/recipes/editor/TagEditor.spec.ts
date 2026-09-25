import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import TagEditor from './TagEditor.svelte';
import { tagSuggestions } from '$features/recipes/stores/tagSuggestions.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * A recipe's tags, edited.
 *
 * What can go wrong here is the seam between the two spellings a tag has: a
 * recipe carries slugs, a person types names, and the server turns a name into
 * a slug the first time it is saved. A tag that shows twice, or a suggestion
 * that adds a second spelling of a tag the kitchen already has, is that seam
 * showing.
 */
const household = [
  { slug: 'italienisch', name: 'italienisch', recipeCount: 7 },
  { slug: 'ofengericht', name: 'Ofengericht', recipeCount: 3 }
];

function render(tags: string[], suggestions = [] as { name: string; slug: string | null }[]) {
  const onchange = vi.fn();

  renderWithProviders(TagEditor, { props: { tags, household, suggestions, onchange } });

  return onchange;
}

describe('the tags a recipe carries', () => {
  it('takes one of the kitchen’s tags off by its slug', async () => {
    const onchange = render(['italienisch', 'ofengericht']);

    await userEvent.click(screen.getByRole('button', { name: /^Ofengericht/ }));

    expect(onchange).toHaveBeenCalledWith(['italienisch']);
  });

  it('adds a new tag by the name that was typed', async () => {
    const onchange = render(['italienisch']);

    await userEvent.type(screen.getByLabelText('New tag'), 'Sonntag{Enter}');

    expect(onchange).toHaveBeenCalledWith(['italienisch', 'Sonntag']);
  });

  it('adds the kitchen’s own tag when its name is typed, not a second spelling of it', async () => {
    const onchange = render([]);

    await userEvent.type(screen.getByLabelText('New tag'), 'Italienisch');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(onchange).toHaveBeenCalledWith(['italienisch']);
  });

  it('shows a tag added since the last save once, and lets it be taken off', async () => {
    const onchange = render(['italienisch', 'Sonntag']);

    await userEvent.click(screen.getByRole('button', { name: /Remove Sonntag/ }));

    expect(onchange).toHaveBeenCalledWith(['italienisch']);
  });
});

describe('the tags it could carry', () => {
  it('adds a suggestion with one tap: the kitchen’s by slug, a new one by name', async () => {
    const onchange = render(
      [],
      [
        { name: 'Ofengericht', slug: 'ofengericht' },
        { name: 'Auflauf', slug: null }
      ]
    );

    await userEvent.click(screen.getByRole('button', { name: '+ Ofengericht' }));
    expect(onchange).toHaveBeenLastCalledWith(['ofengericht']);

    await userEvent.click(screen.getByRole('button', { name: '+ Auflauf' }));
    expect(onchange).toHaveBeenLastCalledWith(['Auflauf']);
  });

  it('never offers what it already carries, however it is spelt', () => {
    render(
      ['Auflauf', 'ofengericht'],
      [
        { name: 'auflauf', slug: null },
        { name: 'Ofengericht', slug: 'ofengericht' }
      ]
    );

    // A suggestion is offered, never applied — and never offered twice.
    expect(screen.queryByRole('button', { name: /^\+ / })).not.toBeInTheDocument();
  });
});

describe('asking for suggestions', () => {
  beforeEach(() => {
    tagSuggestions.reset();
  });

  it('asks once per saved version, and again after the next save', async () => {
    const fetched = vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ items: [{ name: 'Auflauf', slug: null }] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    );

    vi.stubGlobal('fetch', fetched);

    await tagSuggestions.load('r1', 3);
    await tagSuggestions.load('r1', 3);
    await tagSuggestions.load('r1', 4);

    expect(fetched).toHaveBeenCalledTimes(2);
    expect(tagSuggestions.of('r1', 4)).toEqual([{ name: 'Auflauf', slug: null }]);
  });
});
