import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { preferences } from '$shell/preferences.svelte';
import ShoppingAddLine from './ShoppingAddLine.svelte';
import { shopping } from './stores/shopping.svelte';

vi.mock('$features/recipes/stores/ingredients.svelte', () => ({
  ingredients: {
    answered: 'But',
    items: [{ name: 'Butter', own: true, section: 'dairy_eggs' }],
    suggest: vi.fn().mockResolvedValue(undefined)
  }
}));

describe('quick shopping entry', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    preferences.reset();
  });

  it('reads a whole ingredient line and keeps focus for the next item', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const add = vi.spyOn(shopping, 'add').mockResolvedValue(undefined);
    render(ShoppingAddLine, { householdId: 'h1', onaddrecipe: vi.fn() });
    const input = screen.getByRole('combobox', { name: 'What to buy' });

    await userEvent.type(input, '1/2 kg Mehl{Enter}');

    expect(add).toHaveBeenCalledWith('h1', 'Mehl', 0.5, 'kg');
    expect(input).toHaveValue('');
    expect(input).toHaveFocus();
  });

  it('retains the written amount when choosing a suggested ingredient', async () => {
    preferences.adopt({ locale: 'en' }, { signedIn: false });
    const add = vi.spyOn(shopping, 'add').mockResolvedValue(undefined);
    render(ShoppingAddLine, { householdId: 'h1', onaddrecipe: vi.fn() });
    const input = screen.getByRole('combobox', { name: 'What to buy' });

    await userEvent.type(input, '200 g But');
    await screen.findByRole('option', { name: 'Butter' });
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(input).toHaveValue('200 g Butter');
    expect(add).not.toHaveBeenCalled();
    await userEvent.keyboard('{Enter}');

    expect(add).toHaveBeenCalledWith('h1', 'Butter', 200, 'g');
  });
});
