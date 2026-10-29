import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import SwitchHarness from '../__fixtures__/SwitchHarness.svelte';
import { renderWithProviders } from '#lib/test/render.js';

const toggle = () => screen.getByRole('switch', { name: 'Open registration' });

describe('Switch', () => {
  it('carries its label, which a <label for> could not give a button', () => {
    renderWithProviders(SwitchHarness, {});

    expect(toggle()).toBeInTheDocument();
  });

  it('flips on press and on space, and reports the new state', async () => {
    const onchange = vi.fn();

    renderWithProviders(SwitchHarness, { props: { onchange } });

    await userEvent.click(toggle());
    expect(onchange).toHaveBeenLastCalledWith(true);
    expect(toggle()).toBeChecked();

    await userEvent.keyboard(' ');
    expect(onchange).toHaveBeenLastCalledWith(false);
    expect(toggle()).not.toBeChecked();
  });

  it('is described by its explanation, so the label can stay short', () => {
    renderWithProviders(SwitchHarness, {
      props: { description: 'Anyone with the address can create an account.' }
    });

    expect(toggle()).toHaveAccessibleDescription('Anyone with the address can create an account.');
  });

  it('keeps naming the switch when a row above it already prints the label', () => {
    renderWithProviders(SwitchHarness, { props: { hideLabel: true } });

    expect(toggle()).toBeInTheDocument();
    expect(screen.getByText('Open registration').parentElement).toHaveClass('ds-clipped');
  });
});
