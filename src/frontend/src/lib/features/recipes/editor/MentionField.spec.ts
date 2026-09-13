import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import MentionHarness from './__fixtures__/MentionHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

const step = () => screen.getByRole('combobox', { name: 'Step 1' });
const picker = () => screen.queryByRole('listbox', { name: 'Ingredients to mention' });

describe('mentioning an ingredient in a step', () => {
  it('suggests nothing until an @ is typed', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Melt the butter');

    expect(picker()).not.toBeInTheDocument();
  });

  it('offers the recipe’s ingredients, with their amounts', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Melt @');

    expect(screen.getByRole('option', { name: /butter/ })).toHaveTextContent('200 g');
    expect(screen.getByRole('option', { name: /olive oil/ })).toBeInTheDocument();
  });

  it('narrows to what is typed', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Whisk @oli');

    expect(screen.queryByRole('option', { name: /^butter/ })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /olive oil/ })).toBeInTheDocument();
  });

  it('writes the chosen name in, using the keyboard alone', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Melt @but');
    await userEvent.keyboard('{Enter}');

    expect(step()).toHaveValue('Melt @butter ');
    expect(picker()).not.toBeInTheDocument();
    expect(step()).toHaveFocus();
  });

  it('walks the list with the arrow keys', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Add @');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(step()).toHaveValue('Add @olive oil ');
  });

  it('closes on Escape without writing anything', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Melt @but');
    await userEvent.keyboard('{Escape}');

    expect(picker()).not.toBeInTheDocument();
    expect(step()).toHaveValue('Melt @but');
  });

  it('stops offering once the words are no longer a name', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Add @olive oil');
    expect(picker()).toBeInTheDocument();

    await userEvent.type(step(), ' to the pan');
    expect(picker()).not.toBeInTheDocument();
  });

  it('adds an ingredient the recipe does not have yet, from inside the sentence', async () => {
    const onadd = vi.fn();

    renderWithProviders(MentionHarness, { props: { onadd } });

    await userEvent.type(step(), 'Sprinkle @saffron');
    await userEvent.click(screen.getByRole('option', { name: 'Add “saffron” to the ingredients' }));

    expect(onadd).toHaveBeenCalledWith('saffron');
    expect(step()).toHaveValue('Sprinkle @saffron ');
  });

  it('does not offer to add a name the recipe already has', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Melt @butter');

    expect(screen.queryByRole('option', { name: /Add/ })).not.toBeInTheDocument();
  });

  it('tells the textarea which suggestion the arrows have landed on', async () => {
    renderWithProviders(MentionHarness, {});

    await userEvent.type(step(), 'Add @');

    expect(step()).toHaveAttribute('aria-expanded', 'true');
    expect(step()).toHaveAttribute('aria-activedescendant', 'step-0-mentions-0');

    await userEvent.keyboard('{ArrowDown}');

    expect(step()).toHaveAttribute('aria-activedescendant', 'step-0-mentions-1');
  });
});
