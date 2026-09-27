import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';

import ScaleToAmountSheet from './ScaleToAmountSheet.svelte';
import { renderWithProviders } from '$lib/test/render';
import type { RecipeReading } from '../types';

const recipe: RecipeReading = {
  id: 'r1',
  title: 'Tomato sauce',
  description: null,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [
    {
      id: 'g1',
      name: null,
      ingredients: [
        { id: 'i-tomatoes', name: 'tomatoes', note: null, quantity: { value: 600, unit: 'g' } }
      ]
    }
  ],
  steps: [],
  tags: [],
  sourceUrl: null,
  updatedAt: '2026-09-18T00:00:00Z'
};

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };
});

const amount = () => screen.getByRole('textbox', { name: 'Amount' });
const apply = () => screen.getByRole('button', { name: 'Scale the recipe' });

describe('scaling to what is in the cupboard', () => {
  it('suggests the ingredient’s own amount as the example', () => {
    renderWithProviders(ScaleToAmountSheet, { props: { open: true, recipe, onapply: () => {} } });

    expect(amount()).toHaveAttribute('placeholder', '600\u00a0g');
  });

  it('reads a bare number in the ingredient’s own unit, and says so', async () => {
    renderWithProviders(ScaleToAmountSheet, { props: { open: true, recipe, onapply: () => {} } });

    await userEvent.type(amount(), '300');

    expect(screen.getByText(/^Read as 300\sg\.$/)).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('That makes 2 servings.');
    expect(apply()).toBeEnabled();
  });

  it('takes a unit that converts', async () => {
    renderWithProviders(ScaleToAmountSheet, { props: { open: true, recipe, onapply: () => {} } });

    await userEvent.type(amount(), '1.2 kg');

    expect(screen.getByRole('status')).toHaveTextContent('That makes 8 servings.');
  });

  it('explains a unit it will not convert, rather than guessing', async () => {
    renderWithProviders(ScaleToAmountSheet, { props: { open: true, recipe, onapply: () => {} } });

    await userEvent.type(amount(), '300 ml');

    expect(screen.getByRole('alert')).toHaveTextContent(
      'This recipe measures it in g, and that amount does not convert to it. Enter it in g.'
    );
    expect(amount()).toHaveAttribute('aria-invalid', 'true');
    expect(apply()).toBeDisabled();
  });

  it('asks for a number when there is none', async () => {
    renderWithProviders(ScaleToAmountSheet, { props: { open: true, recipe, onapply: () => {} } });

    await userEvent.type(amount(), 'some');

    expect(screen.getByRole('alert')).toHaveTextContent(
      /^Enter how much you have, such as 600\sg\.$/
    );
  });
});
