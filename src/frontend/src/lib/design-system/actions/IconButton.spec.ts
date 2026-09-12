import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import IconOnly from '../__fixtures__/IconOnly.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * An icon-only control with no accessible name is announced as "button" and
 * nothing else, which makes it unusable rather than merely unlabelled.
 */
describe('IconButton', () => {
  it('is found by the name it is given, not by its icon', () => {
    renderWithProviders(IconOnly, {});

    expect(screen.getByRole('button', { name: 'Add to shopping list' })).toBeInTheDocument();
  });

  it('hides the icon from assistive technology, so the name is not read twice', () => {
    const { container } = renderWithProviders(IconOnly, {});

    expect(container.querySelector('svg')?.parentElement).toHaveAttribute('aria-hidden', 'true');
  });

  it('announces a toggle as pressed rather than as a new control', () => {
    renderWithProviders(IconOnly, { props: { pressed: true } });

    expect(screen.getByRole('button', { name: 'Add to shopping list' })).toHaveAttribute(
      'aria-pressed',
      'true'
    );
  });

  it('says nothing about pressed state when it is not a toggle', () => {
    renderWithProviders(IconOnly, {});

    expect(screen.getByRole('button')).not.toHaveAttribute('aria-pressed');
  });

  it('does nothing when disabled', async () => {
    const onclick = vi.fn();

    renderWithProviders(IconOnly, { props: { onclick, disabled: true } });

    await userEvent.click(screen.getByRole('button'));

    expect(onclick).not.toHaveBeenCalled();
  });
});
