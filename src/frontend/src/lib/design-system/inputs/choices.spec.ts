import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import ChoiceHarness from '../__fixtures__/ChoiceHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * These four are native elements on purpose. What is worth asserting is that
 * they are still native — reachable by keyboard, announced by role — and that
 * the props each one adds behave.
 */
describe('Checkbox', () => {
  it('is toggled by its label, so the whole row is the target', async () => {
    const onchecked = vi.fn();

    renderWithProviders(ChoiceHarness, { props: { onchecked } });

    await userEvent.click(screen.getByText('Vegetarian'));

    expect(onchecked).toHaveBeenLastCalledWith(true);
    expect(screen.getByRole('checkbox', { name: 'Vegetarian' })).toBeChecked();
  });

  it('shows a partial selection as partial, not as unchecked', () => {
    renderWithProviders(ChoiceHarness, { props: { indeterminate: true } });

    expect(screen.getByRole('checkbox', { name: 'Vegetarian' })).toBePartiallyChecked();
  });

  it('does nothing when disabled', async () => {
    const onchecked = vi.fn();

    renderWithProviders(ChoiceHarness, { props: { onchecked, checkboxDisabled: true } });

    await userEvent.click(screen.getByText('Vegetarian'));

    expect(onchecked).not.toHaveBeenCalled();
  });
});

describe('RadioGroup', () => {
  it('moves between options with the arrow keys, as native radios do', async () => {
    const oncourse = vi.fn();

    renderWithProviders(ChoiceHarness, { props: { oncourse } });

    screen.getByRole('radio', { name: 'Main' }).focus();
    await userEvent.keyboard('{ArrowUp}');

    expect(oncourse).toHaveBeenLastCalledWith('starter');
  });

  it('skips an option that cannot be chosen', () => {
    renderWithProviders(ChoiceHarness, {});

    expect(screen.getByRole('radio', { name: 'Dessert' })).toBeDisabled();
  });
});

describe('Select', () => {
  it('reports the chosen value', async () => {
    const onunit = vi.fn();

    renderWithProviders(ChoiceHarness, { props: { onunit } });

    await userEvent.selectOptions(screen.getByRole('combobox'), 'imperial');

    expect(onunit).toHaveBeenLastCalledWith('imperial');
  });
});

describe('TextArea', () => {
  it('accepts several lines without a scrollbar inside the field', async () => {
    renderWithProviders(ChoiceHarness, {});

    const area = screen.getByRole('textbox', { name: '' });

    await userEvent.type(area, 'one{enter}two{enter}three');

    expect(area).toHaveValue('one\ntwo\nthree');
    expect(getComputedStyle(area).overflow).toBe('hidden');
  });
});
