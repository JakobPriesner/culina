import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import StepIngredients from './StepIngredients.svelte';
import type { Ingredient, Step } from '../types';
import { renderWithProviders } from '$lib/test/render';

/* The chip row says what a sentence cannot ("combine everything and knead" names none of the five things it needs). */

const butter: Ingredient = {
  id: 'i-butter',
  quantity: { value: 200, unit: 'g' },
  name: 'butter',
  note: null
};

const salt: Ingredient = {
  id: 'i-salt',
  quantity: { value: null, unit: null },
  name: 'salt',
  note: null
};

/** A line the server has not seen yet, so nothing can point at it. */
const unsaved: Ingredient = {
  id: '',
  quantity: { value: null, unit: null },
  name: 'yeast',
  note: null
};

const step = (uses: string[], mentions: string[] = []): Step => ({
  id: 's1',
  title: null,
  segments: [
    { kind: 'text', text: 'Combine.' },
    ...mentions.map(
      (id) =>
        ({
          kind: 'ingredient',
          ingredientId: id,
          name: id,
          quantity: { value: null, unit: null }
        }) as const
    )
  ],
  uses,
  durationSeconds: null
});

const render = (one: Step, ingredients: Ingredient[], onchange = vi.fn()) => {
  renderWithProviders(StepIngredients, {
    props: { step: one, number: 1, ingredients, onchange }
  });

  return onchange;
};

const chips = () => within(screen.getByRole('list'));

/** The picker, read while closed: opening is the browser's `popover`, which jsdom lacks and which is not this component's business. */
const picker = () => ({
  option: (name: string) => screen.getByRole('checkbox', { name, hidden: true }),
  missing: (name: string) => screen.queryByRole('checkbox', { name, hidden: true })
});

describe('what a step needs', () => {
  it('shows an ingredient the words never name', () => {
    render(step(['i-salt']), [butter, salt]);

    expect(chips().getByText('salt')).toBeInTheDocument();
    expect(chips().queryByText('butter')).not.toBeInTheDocument();
  });

  it('shows the amount beside it, so the chip means something', () => {
    render(step(['i-butter']), [butter, salt]);

    expect(chips().getByText('200 g')).toBeInTheDocument();
  });

  it('adds one, and writes the list back in the recipe’s order', async () => {
    const onchange = render(step(['i-salt']), [butter, salt]);

    await userEvent.click(picker().option('butter'));

    // Butter first because it is first in the list, not because it was ticked last, or chips shuffle as you pick.
    expect(onchange).toHaveBeenLastCalledWith(['i-butter', 'i-salt']);
  });

  it('takes one off again', async () => {
    const onchange = render(step(['i-butter', 'i-salt']), [butter, salt]);

    await userEvent.click(screen.getByRole('button', { name: 'Take salt off this step' }));

    expect(onchange).toHaveBeenLastCalledWith(['i-butter']);
  });
});

describe('an ingredient the sentence names', () => {
  it('cannot be taken off here, because the words are what put it there', () => {
    render(step(['i-butter'], ['i-butter']), [butter, salt]);

    // A remove button the server would undo on the next save is worse than none.
    expect(chips().getByText('butter')).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Take butter off this step' })
    ).not.toBeInTheDocument();
  });

  it('is locked in the picker too', () => {
    render(step(['i-butter'], ['i-butter']), [butter, salt]);

    expect(picker().option('butter')).toBeDisabled();
  });
});

describe('a line the server has not seen yet', () => {
  it('is not offered, because a step cannot point at something with no id', () => {
    render(step([]), [butter, unsaved]);

    expect(picker().option('butter')).toBeInTheDocument();
    expect(picker().missing('yeast')).not.toBeInTheDocument();
  });
});
