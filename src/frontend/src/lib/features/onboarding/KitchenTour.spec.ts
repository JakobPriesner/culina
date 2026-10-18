import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '$lib/test/render';

import KitchenTour from './KitchenTour.svelte';

/* Five cards, a minute: the loop, capturing, writing steps, planning and shopping, cooking. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.open = false;
  };
});

describe('the tour of Culina', () => {
  it('walks through the loop card by card, and ends where cooking starts', async () => {
    const onclose = vi.fn();

    renderWithProviders(KitchenTour, { props: { open: true, onclose } });

    expect(screen.getByRole('heading', { name: 'Welcome to your kitchen' })).toBeInTheDocument();
    expect(screen.getByText('Shopping')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));

    // The step card shows the rule working: written with @, cooked with the amounts in.
    expect(
      screen.getByRole('heading', { name: 'Steps that help while cooking' })
    ).toBeInTheDocument();
    expect(screen.getByText('20 g butter')).toBeInTheDocument();
    expect(screen.getByText(/Type @ to reference an ingredient/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Let’s cook' }));

    expect(onclose).toHaveBeenCalledOnce();
  });

  it('can be skipped from the first card', async () => {
    const onclose = vi.fn();

    renderWithProviders(KitchenTour, { props: { open: true, onclose } });

    await userEvent.click(screen.getAllByRole('button', { name: 'Skip the tour' })[0]!);

    expect(onclose).toHaveBeenCalled();
  });
});
