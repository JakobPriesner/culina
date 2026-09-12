import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import FourStates from '../__fixtures__/FourStates.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * A list is in one of four states, and three of them are the ones that get
 * built last and tested never.
 */
describe('while loading', () => {
  it('shows the shape of what is coming, and says it is busy', () => {
    const { container } = renderWithProviders(FourStates, { props: { state: 'loading' } });

    expect(screen.getByLabelText('Loading recipes')).toHaveAttribute('aria-busy', 'true');
    expect(container.querySelectorAll('.skeleton')).toHaveLength(3);
  });

  it('hides the placeholders from assistive technology', () => {
    const { container } = renderWithProviders(FourStates, { props: { state: 'loading' } });

    for (const block of container.querySelectorAll('.skeleton')) {
      expect(block).toHaveAttribute('aria-hidden', 'true');
    }
  });
});

describe('when there is nothing yet', () => {
  it('says why, and offers the thing to do about it', () => {
    renderWithProviders(FourStates, { props: { state: 'empty' } });

    expect(screen.getByRole('heading', { name: 'No recipes yet' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add a recipe' })).toBeInTheDocument();
  });
});

describe('when a filter matched nothing', () => {
  it('offers to undo the filter rather than to create something', async () => {
    const onclear = vi.fn();

    renderWithProviders(FourStates, { props: { state: 'filtered', onclear } });

    await userEvent.click(screen.getByRole('button', { name: 'Clear the filter' }));

    expect(onclear).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: 'Add a recipe' })).not.toBeInTheDocument();
  });
});

describe('when it failed', () => {
  it('announces itself, explains in plain language, and offers to retry', async () => {
    const onretry = vi.fn();

    renderWithProviders(FourStates, { props: { state: 'error', onretry } });

    expect(screen.getByRole('alert')).toHaveTextContent('Could not load your recipes');

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(onretry).toHaveBeenCalledOnce();
  });

  it('shows the reference that ties the report to a log line', () => {
    renderWithProviders(FourStates, { props: { state: 'error' } });

    expect(screen.getByText('req-4f2a')).toBeInTheDocument();
  });
});

describe('when refreshing something already on screen', () => {
  it('keeps the data visible rather than replacing it with a skeleton', () => {
    const { container } = renderWithProviders(FourStates, {
      props: { state: 'loaded', refreshing: true }
    });

    expect(screen.getByText('Tomato soup')).toBeInTheDocument();
    expect(container.querySelector('.skeleton')).toBeNull();
    expect(screen.getByLabelText('Refreshing recipes')).toHaveAttribute('aria-busy', 'true');
  });

  it('claims nothing while it is not refreshing', () => {
    renderWithProviders(FourStates, { props: { state: 'loaded' } });

    expect(screen.getByText('Tomato soup')).toBeInTheDocument();
    expect(screen.queryByLabelText('Refreshing recipes')).not.toBeInTheDocument();
  });
});
