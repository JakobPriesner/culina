import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import SearchHarness from '../__fixtures__/SearchHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

const clear = () => screen.queryByRole('button', { name: 'Clear search' });

describe('SearchField', () => {
  it('offers no way to clear an empty field', () => {
    renderWithProviders(SearchHarness, {});

    expect(clear()).not.toBeInTheDocument();
  });

  it('reports what is typed', async () => {
    const oninput = vi.fn();

    renderWithProviders(SearchHarness, { props: { oninput } });

    await userEvent.type(screen.getByRole('searchbox', { name: 'Search recipes' }), 'soup');

    expect(oninput).toHaveBeenLastCalledWith('soup');
  });

  it('clears with Escape and keeps keyboard focus', async () => {
    const oninput = vi.fn();
    renderWithProviders(SearchHarness, { props: { value: 'soup', oninput } });
    const box = screen.getByRole('searchbox', { name: 'Search recipes' });
    box.focus();
    await userEvent.keyboard('{Escape}');
    expect(box).toHaveValue('');
    expect(box).toHaveFocus();
    expect(oninput).toHaveBeenLastCalledWith('');
  });

  it('clears, and leaves focus where the typing was', async () => {
    const oninput = vi.fn();

    renderWithProviders(SearchHarness, { props: { value: 'soup', oninput } });

    await userEvent.click(clear()!);

    const box = screen.getByRole('searchbox', { name: 'Search recipes' });

    expect(box).toHaveValue('');
    expect(box).toHaveFocus();
    expect(oninput).toHaveBeenLastCalledWith('');
  });
});
