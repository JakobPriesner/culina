import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import IngredientHarness from './__fixtures__/IngredientHarness.svelte';
import { ingredients as known } from '../stores/ingredients.svelte';
import { units } from '../stores/units.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * An ingredient is written as the three things it is made of, so these are
 * about the three things: that an amount, a unit and a name entered apart stay
 * apart, and that neither list can stop somebody writing a word it has never
 * heard of.
 */

const amount = () => screen.getByLabelText('Amount');
const unit = () => screen.getByRole('combobox', { name: 'Unit' });
const name = () => screen.getByRole('combobox', { name: 'Ingredient' });
const note = () => screen.getByLabelText('Preparation');
const unitList = () => screen.queryByRole('listbox', { name: 'Unit suggestions' });
const nameList = () => screen.queryByRole('listbox', { name: 'Ingredient suggestions' });

/** The suggestions endpoint, answering with whatever this kitchen is told. */
function serverSuggests(items: { name: string; section: string; own: boolean }[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      () =>
        new Response(JSON.stringify({ items }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        })
    )
  );
}

beforeEach(() => {
  serverSuggests([]);
});

afterEach(() => {
  vi.unstubAllGlobals();
  known.clear();
  units.reset();
});

describe('writing an ingredient', () => {
  it('keeps the amount, the unit and the name apart', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(amount(), '200');
    await userEvent.type(unit(), 'g');
    await userEvent.type(name(), 'flour');
    await userEvent.click(screen.getByRole('button', { name: 'Add' }));

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: 200, unit: 'g' }, name: 'flour', note: null }
    ]);
  });

  it('reads a comma as a decimal point, the way half of Europe types one', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(amount(), '1,5');
    await userEvent.type(unit(), 'kg');
    await userEvent.type(name(), 'Mehl');
    await userEvent.keyboard('{Enter}');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: 1.5, unit: 'kg' }, name: 'Mehl', note: null }
    ]);
  });

  it('leaves the amount out when the recipe does not give one', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(name(), 'salt');
    await userEvent.keyboard('{Enter}');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: null, unit: null }, name: 'salt', note: null }
    ]);
  });

  it('takes a preparation alongside the name', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(amount(), '2');
    await userEvent.type(name(), 'onions');
    await userEvent.type(note(), 'finely chopped');
    await userEvent.keyboard('{Enter}');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: 2, unit: null }, name: 'onions', note: 'finely chopped' }
    ]);
  });

  it('adds nothing without a name, because a bare amount says nothing', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(amount(), '200');
    await userEvent.keyboard('{Enter}');

    expect(onchange).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled();
  });

  it('empties the fields and hands the cursor back, ready for the next', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(amount(), '200');
    await userEvent.type(unit(), 'g');
    await userEvent.type(name(), 'flour');
    await userEvent.keyboard('{Enter}');

    expect(amount()).toHaveValue('');
    expect(unit()).toHaveValue('');
    expect(name()).toHaveValue('');
    expect(amount()).toHaveFocus();
  });

  it('reads the written ingredient back as one line', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(amount(), '200');
    await userEvent.type(unit(), 'g');
    await userEvent.type(name(), 'flour');
    await userEvent.keyboard('{Enter}');

    const row = screen.getByRole('listitem');

    expect(row).toHaveTextContent('200');
    expect(row).toHaveTextContent('flour');
  });
});

describe('the unit list', () => {
  it('offers the built-in units, by their names rather than their codes', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(unit(), 'p');

    expect(unitList()).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'pinch' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'piece' })).toBeInTheDocument();
  });

  it('narrows to what is typed', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(unit(), 'sl');

    expect(screen.getByRole('option', { name: 'slice' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'pinch' })).not.toBeInTheDocument();
  });

  it('writes a chosen unit in, using the keyboard alone', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(unit(), 'sl');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(unit()).toHaveValue('slice');
    expect(unitList()).not.toBeInTheDocument();
  });

  it('lets a unit nobody has written before be added by writing it', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(amount(), '1');
    await userEvent.type(unit(), 'Schuss');
    await userEvent.type(name(), 'Milch');
    await userEvent.keyboard('{Enter}');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: 1, unit: 'Schuss' }, name: 'Milch', note: null }
    ]);
    // And it is a unit this kitchen measures in from now on.
    expect(units.all).toContain('Schuss');
  });

  it('does not remember the halves of a unit still being typed', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(unit(), 'Schuss');
    await userEvent.type(name(), 'Milch');
    await userEvent.keyboard('{Enter}');

    expect(units.own).toEqual(['Schuss']);
  });

  it('closes on Escape without writing anything', async () => {
    renderWithProviders(IngredientHarness, {});

    await userEvent.type(unit(), 'sl');
    await userEvent.keyboard('{Escape}');

    expect(unitList()).not.toBeInTheDocument();
    expect(unit()).toHaveValue('sl');
  });
});

describe('the ingredient list', () => {
  it('offers what this kitchen calls things, with where they are found', async () => {
    serverSuggests([{ name: 'flour', section: 'baking', own: false }]);

    renderWithProviders(IngredientHarness, {});

    await userEvent.type(name(), 'flo');
    await vi.waitFor(() => expect(nameList()).toBeInTheDocument());

    expect(within(nameList()!).getByRole('option', { name: /flour/ })).toBeInTheDocument();
  });

  it('writes a chosen name in', async () => {
    serverSuggests([{ name: 'flour', section: 'baking', own: true }]);

    renderWithProviders(IngredientHarness, {});

    await userEvent.type(name(), 'flo');
    await vi.waitFor(() => expect(nameList()).toBeInTheDocument());
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(name()).toHaveValue('flour');
  });

  it('adds what was typed when no row was chosen, however long the list', async () => {
    serverSuggests([{ name: 'flour', section: 'baking', own: true }]);

    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, { props: { onchange } });

    await userEvent.type(name(), 'flo');
    await vi.waitFor(() => expect(nameList()).toBeInTheDocument());
    await userEvent.keyboard('{Enter}');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: '', quantity: { value: null, unit: null }, name: 'flo', note: null }
    ]);
  });
});

describe('correcting an ingredient', () => {
  const existing = [
    {
      id: 'i-1',
      quantity: { value: 200, unit: 'g' },
      name: 'flour',
      note: null
    }
  ];

  /** The open row, named so it is not confused with the one below it. */
  const row = () => within(screen.getByRole('group', { name: 'Correct flour' }));

  it('opens the same fields it was written in, already filled', async () => {
    renderWithProviders(IngredientHarness, { props: { ingredients: [...existing] } });

    await userEvent.click(screen.getByRole('button', { name: 'Correct flour' }));

    expect(row().getByLabelText('Amount')).toHaveValue('200');
    expect(row().getByRole('combobox', { name: 'Unit' })).toHaveValue('g');
    expect(row().getByRole('combobox', { name: 'Ingredient' })).toHaveValue('flour');
  });

  it('leaves the empty row below it alone, so the two cannot be confused', async () => {
    renderWithProviders(IngredientHarness, { props: { ingredients: [...existing] } });

    await userEvent.click(screen.getByRole('button', { name: 'Correct flour' }));

    const adding = within(screen.getByRole('group', { name: 'New ingredient' }));

    expect(adding.getByLabelText('Amount')).toHaveValue('');
    expect(adding.getByRole('combobox', { name: 'Ingredient' })).toHaveValue('');
  });

  it('writes a correction through, keeping the ingredient\u2019s identity', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, {
      props: { ingredients: [...existing], onchange }
    });

    await userEvent.click(screen.getByRole('button', { name: 'Correct flour' }));
    await userEvent.clear(row().getByLabelText('Amount'));
    await userEvent.type(row().getByLabelText('Amount'), '250');

    expect(onchange).toHaveBeenLastCalledWith([
      { id: 'i-1', quantity: { value: 250, unit: 'g' }, name: 'flour', note: null }
    ]);
  });

  it('removes an ingredient', async () => {
    const onchange = vi.fn();

    renderWithProviders(IngredientHarness, {
      props: { ingredients: [...existing], onchange }
    });

    await userEvent.click(screen.getByRole('button', { name: 'Remove flour' }));

    expect(onchange).toHaveBeenLastCalledWith([]);
  });
});

describe('where an ingredient ends up', () => {
  const flour = {
    id: 'i-1',
    quantity: { value: 200, unit: 'g' },
    name: 'flour',
    note: null
  };

  const step = (uses: string[]) => ({
    id: null,
    segments: [{ kind: 'text' as const, text: 'Combine.' }],
    uses,
    durationSeconds: null
  });

  it('says which steps need it, numbered as the editor labels them', () => {
    renderWithProviders(IngredientHarness, {
      props: { ingredients: [flour], steps: [step(['i-1']), step([]), step(['i-1'])] }
    });

    expect(screen.getByRole('button', { name: 'Go to step 1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Go to step 3' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Go to step 2' })).not.toBeInTheDocument();
  });

  it('says quietly that it is in none of them', () => {
    // Salt to taste belongs to no step. Worth knowing, never an error.
    renderWithProviders(IngredientHarness, {
      props: { ingredients: [flour], steps: [step([])] }
    });

    expect(screen.getByText('Not in a step')).toBeInTheDocument();
  });

  it('says nothing about a line the server has not seen yet', () => {
    // No id means no step could point at it, so "not in a step" would be
    // telling somebody off for not having saved.
    renderWithProviders(IngredientHarness, {
      props: { ingredients: [{ ...flour, id: '' }], steps: [step([])] }
    });

    expect(screen.queryByText('Not in a step')).not.toBeInTheDocument();
  });
});
