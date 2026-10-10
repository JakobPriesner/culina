import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import IngredientGroups from './IngredientGroups.svelte';
import type { IngredientGroup } from '../types';

const line = (name: string) => ({
  id: name,
  name,
  note: null,
  quantity: { value: null, unit: null }
});

const props = (groups: IngredientGroup[]) => ({
  groups,
  steps: [],
  onchange: vi.fn(),
  onrename: vi.fn(),
  onadd: vi.fn(),
  onremove: vi.fn(),
  householdId: 'h1',
  language: 'en'
});

const named: IngredientGroup[] = [
  { id: 'g1', name: 'For the dough', ingredients: [line('Flour')] },
  { id: 'g2', name: 'For the filling', ingredients: [line('Apples')] }
];

describe('the ingredient groups', () => {
  it('shows every group with its name and its lines', () => {
    renderWithProviders(IngredientGroups, { props: props(named) });

    expect(screen.getByRole('textbox', { name: 'Name of group 1' })).toHaveValue('For the dough');
    expect(screen.getByRole('textbox', { name: 'Name of group 2' })).toHaveValue('For the filling');
    expect(screen.getByText('Flour')).toBeInTheDocument();
    expect(screen.getByText('Apples')).toBeInTheDocument();
  });

  it('shows no heading for a single unnamed group', () => {
    renderWithProviders(IngredientGroups, {
      props: props([{ id: 'g1', name: null, ingredients: [line('Flour')] }])
    });

    expect(screen.queryByRole('textbox', { name: /Name of group/ })).toBeNull();
  });

  it('keeps the add row open only in the last group', () => {
    renderWithProviders(IngredientGroups, { props: props(named) });

    expect(screen.getAllByRole('combobox', { name: 'Ingredient' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: /Add ingredient to group 1/ })).toBeInTheDocument();
  });

  it('adds a line to the group whose add row was used', async () => {
    const given = props(named);

    renderWithProviders(IngredientGroups, { props: given });

    await userEvent.click(screen.getByRole('button', { name: /Add ingredient to group 1/ }));

    const field = screen.getAllByRole('combobox', { name: 'Ingredient' })[0]!;

    await userEvent.type(field, 'Butter{Enter}');

    expect(given.onchange).toHaveBeenLastCalledWith(0, [
      line('Flour'),
      { id: '', quantity: { value: null, unit: null }, name: 'Butter', note: null }
    ]);
  });

  it('reports a rename', async () => {
    const given = props(named);

    renderWithProviders(IngredientGroups, { props: given });

    await userEvent.type(screen.getByRole('textbox', { name: 'Name of group 2' }), '!');

    expect(given.onrename).toHaveBeenLastCalledWith(1, 'For the filling!');
  });

  it('offers to remove a group only while it is empty', async () => {
    const given = props([...named, { id: 'g3', name: null, ingredients: [] }]);

    renderWithProviders(IngredientGroups, { props: given });

    expect(screen.getAllByRole('button', { name: /Remove empty group/ })).toHaveLength(1);

    await userEvent.click(screen.getByRole('button', { name: 'Remove empty group 3' }));

    expect(given.onremove).toHaveBeenCalledWith(2);
  });

  it('asks for a new group', async () => {
    const given = props(named);

    renderWithProviders(IngredientGroups, { props: given });

    await userEvent.click(within(document.body).getByRole('button', { name: '+ Group' }));

    expect(given.onadd).toHaveBeenCalledOnce();
  });
});
