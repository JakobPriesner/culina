import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import StepperHarness from '../__fixtures__/StepperHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

const servings = () => screen.getByRole('spinbutton', { name: 'Servings' });

describe('Stepper', () => {
  it('steps up and down', async () => {
    const onchange = vi.fn();

    renderWithProviders(StepperHarness, { props: { onchange } });

    await userEvent.click(screen.getByRole('button', { name: 'One more serving' }));
    expect(onchange).toHaveBeenLastCalledWith(5);

    await userEvent.click(screen.getByRole('button', { name: 'One fewer serving' }));
    expect(onchange).toHaveBeenLastCalledWith(4);
  });

  it('stops at the ends rather than going past them', () => {
    renderWithProviders(StepperHarness, { props: { value: 1, min: 1, max: 1 } });

    expect(screen.getByRole('button', { name: 'One fewer serving' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'One more serving' })).toBeDisabled();
  });

  it('clamps a typed value, so a slip does not become a wedding', async () => {
    const onchange = vi.fn();

    renderWithProviders(StepperHarness, { props: { onchange, max: 12 } });

    await userEvent.clear(servings());
    await userEvent.type(servings(), '500');
    await userEvent.tab();

    expect(onchange).toHaveBeenLastCalledWith(12);
  });

  it('can still be typed into, for the person who wants eighteen', async () => {
    const onchange = vi.fn();

    renderWithProviders(StepperHarness, { props: { onchange } });

    await userEvent.clear(servings());
    await userEvent.type(servings(), '18');
    await userEvent.tab();

    expect(onchange).toHaveBeenLastCalledWith(18);
  });
});
