import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import LabelledButton from '../__fixtures__/LabelledButton.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * Everything here is about the two ways a button goes wrong: it stops being a
 * button (a div nobody can reach), or it lies about whether it can be pressed.
 */
describe('Button', () => {
  it('is a real button, so a keyboard can reach and press it', async () => {
    const onclick = vi.fn();

    renderWithProviders(LabelledButton, { props: { onclick } });

    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Save' })).toHaveFocus();

    await userEvent.keyboard('{Enter}');
    await userEvent.keyboard(' ');

    expect(onclick).toHaveBeenCalledTimes(2);
  });

  it('is a link when it navigates, because navigation belongs in an anchor', () => {
    renderWithProviders(LabelledButton, { props: { href: '/recipes', label: 'All recipes' } });

    expect(screen.getByRole('link', { name: 'All recipes' })).toHaveAttribute('href', '/recipes');
  });

  it('does nothing when disabled', async () => {
    const onclick = vi.fn();

    renderWithProviders(LabelledButton, { props: { onclick, disabled: true } });

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onclick).not.toHaveBeenCalled();
  });

  it('stays labelled and says it is busy while loading', () => {
    renderWithProviders(LabelledButton, { props: { loading: true } });

    const button = screen.getByRole('button', { name: 'Save' });

    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(button).toHaveAttribute('aria-disabled', 'true');
  });

  it('keeps its place in the tab order while loading, so focus does not jump', async () => {
    renderWithProviders(LabelledButton, { props: { loading: true } });

    await userEvent.tab();

    expect(screen.getByRole('button', { name: 'Save' })).toHaveFocus();
  });

  it('ignores a press while loading', async () => {
    const onclick = vi.fn();

    renderWithProviders(LabelledButton, { props: { onclick, loading: true } });

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onclick).not.toHaveBeenCalled();
  });
});
