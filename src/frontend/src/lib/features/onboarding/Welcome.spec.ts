import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { olliSetting } from '$shell/olli/setting.svelte';
import { renderWithProviders } from '$lib/test/render';

import Welcome from './Welcome.svelte';

/* Olli shows a new cook round in six scenes, each a small working picture to try. Still here: every demo shows how it ends. */
beforeEach(() => {
  olliSetting.animate(false);
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.open = false;
  };
  Element.prototype.scrollIntoView = vi.fn();
});

afterEach(() => {
  olliSetting.reset();
  localStorage.clear();
});

const next = () => userEvent.click(screen.getByRole('button', { name: 'Next' }));

describe('the welcome', () => {
  it('opens with Olli and the loop, each station naming its tab', async () => {
    renderWithProviders(Welcome, { props: { open: true, onclose: vi.fn() } });

    expect(screen.getByRole('heading', { name: /Welcome/ })).toBeInTheDocument();
    expect(screen.getByText(/I’m Olli/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Shop' }));

    expect(screen.getByRole('button', { name: 'Shop' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText(/puts what the week needs on the list/)).toBeInTheDocument();
    // Olli points at where the stations live in the app.
    expect(screen.getByText(/You’ll find these any time in the bar/)).toBeInTheDocument();
  });

  it('brings a collection over from the apps the import page supports', async () => {
    renderWithProviders(Welcome, { props: { open: true, onclose: vi.fn() } });

    await next();
    await userEvent.click(screen.getByRole('button', { name: 'From an app' }));

    expect(screen.getByText(/currently Tandoor and Mealie/)).toBeInTheDocument();
    expect(screen.getByText('3 recipes')).toBeInTheDocument();
    expect(screen.getByText(/into a cookbook named after the app/)).toBeInTheDocument();
  });

  it('shows a step written with @, its amounts following the servings', async () => {
    renderWithProviders(Welcome, { props: { open: true, onclose: vi.fn() } });

    await next();
    await next();

    expect(
      screen.getByRole('heading', { name: /Steps that help while cooking/ })
    ).toBeInTheDocument();
    expect(screen.getByText(/Dice the/)).toHaveTextContent(
      'Dice the 2 onions and soften them in the 20 g butter over a medium heat for 5 minutes.'
    );
    expect(screen.getByText('Timer 5 min')).toBeInTheDocument();
    expect(screen.getByText('One action per step')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'More servings' }));

    expect(screen.getByText(/Dice the/)).toHaveTextContent('Dice the 3 onions');
    // In the step and in its ingredient line alike, with the factor beside the stepper.
    expect(screen.getAllByText('30 g')).toHaveLength(2);
    expect(screen.getByText('×1.5')).toBeInTheDocument();
    expect(screen.getByText(/Exactly!/)).toBeInTheDocument();
  });

  it('writes the list from a planned day, and ticks lines off', async () => {
    renderWithProviders(Welcome, { props: { open: true, onclose: vi.fn() } });

    await userEvent.click(screen.getByRole('button', { name: 'Part 4 of 6' }));

    const week = screen.getByRole('group', { name: 'This week' });
    await userEvent.click(within(week).getAllByRole('button')[4]!);

    const egg = screen.getByRole('button', { name: '4 eggs' });
    expect(screen.getByRole('button', { name: '100 g feta' })).toBeInTheDocument();
    expect(screen.getByText(/Everything the week needs is on the list/)).toBeInTheDocument();

    await userEvent.click(egg);

    expect(egg).toHaveAttribute('aria-pressed', 'true');
  });

  it('ends on the first recipe: a way in, or a look round', async () => {
    const onclose = vi.fn();

    renderWithProviders(Welcome, { props: { open: true, onclose } });

    await userEvent.click(screen.getByRole('button', { name: 'Part 6 of 6' }));

    expect(screen.getByRole('link', { name: /Paste a link or text/ })).toHaveAttribute(
      'href',
      '/recipes/new?start=paste'
    );
    expect(screen.getByRole('link', { name: /Photograph a page/ })).toHaveAttribute(
      'href',
      '/recipes/new?start=photo'
    );
    // No drafting on this instance: the plain form instead of an idea.
    expect(screen.getByRole('link', { name: /Write it yourself/ })).toHaveAttribute(
      'href',
      '/recipes/new'
    );

    expect(screen.getByRole('link', { name: /Import from an app/ })).toHaveAttribute(
      'href',
      '/recipes/import'
    );

    await userEvent.click(screen.getByRole('button', { name: 'Look around first' }));

    expect(onclose).toHaveBeenCalledOnce();
  });

  it('can be skipped from the first scene', async () => {
    const onclose = vi.fn();

    renderWithProviders(Welcome, { props: { open: true, onclose } });

    await userEvent.click(screen.getByRole('button', { name: 'Skip' }));

    expect(onclose).toHaveBeenCalled();
  });
});
