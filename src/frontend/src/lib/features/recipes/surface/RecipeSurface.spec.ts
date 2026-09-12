import { screen, within } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import RecipeSurface from './RecipeSurface.svelte';
import type { Recipe } from '../types';
import { renderWithProviders } from '$lib/test/render';

const butter = 'i-butter';
const flour = 'i-flour';

const recipe: Recipe = {
  id: 'r1',
  householdId: 'h1',
  title: 'Lemon orzo',
  description: null,
  language: 'en',
  yieldAmount: 2,
  yieldKind: 'servings',
  prepMinutes: 10,
  cookMinutes: 15,
  totalMinutes: 25,
  imageId: null,
  tags: ['Weeknight'],
  createdBy: 'u1',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  version: 1,
  groups: [
    {
      id: 'g1',
      name: null,
      ingredients: [
        { id: butter, quantity: { value: 200, unit: 'g' }, name: 'butter', note: null },
        { id: flour, quantity: { value: 300, unit: 'g' }, name: 'flour', note: 'sifted' }
      ]
    }
  ],
  steps: [
    {
      id: 's1',
      durationSeconds: null,
      segments: [
        { kind: 'text', text: 'Melt ' },
        {
          kind: 'ingredient',
          ingredientId: butter,
          name: 'butter',
          quantity: { value: 200, unit: 'g' }
        },
        { kind: 'text', text: ' in the pan.' }
      ]
    },
    {
      id: 's2',
      durationSeconds: null,
      segments: [
        { kind: 'text', text: 'Stir in ' },
        {
          kind: 'ingredient',
          ingredientId: flour,
          name: 'flour',
          quantity: { value: 300, unit: 'g' }
        },
        { kind: 'text', text: '.' }
      ]
    }
  ]
};

const render = (props: Record<string, unknown> = {}) =>
  renderWithProviders(RecipeSurface, { props: { recipe, servings: 2, ...props } });

/** The two regions, because an ingredient's name appears in both. */
const ingredients = () => within(screen.getByRole('region', { name: 'Ingredients' }));
const steps = () => within(screen.getByRole('region', { name: 'Steps' }));

describe('reading a recipe', () => {
  it('shows every ingredient, with its amount', () => {
    render();

    expect(ingredients().getByText('200 g')).toBeInTheDocument();
    expect(ingredients().getByText('300 g')).toBeInTheDocument();
  });

  it('writes the amount into the step, not just the list', () => {
    render();

    // The payoff of storing a reference rather than the words: this is the
    // single most common bug in recipe apps and it cannot happen here.
    expect(screen.getByRole('button', { name: /200\u00a0g butter/ })).toBeInTheDocument();
  });

  it('scales the list and the steps together, from the same number', () => {
    render({ servings: 4 });

    expect(screen.getByRole('button', { name: /400\u00a0g butter/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /600\u00a0g flour/ })).toBeInTheDocument();
  });

  it('admits that the times stop being right when the factor is far from one', () => {
    render({ servings: 6 });

    expect(screen.getByText(/Times are for/)).toBeInTheDocument();
  });

  it('says nothing about times at the recipe’s own yield', () => {
    render();

    expect(screen.queryByText(/Times are for/)).not.toBeInTheDocument();
  });

  it('offers to start cooking', () => {
    render();

    expect(screen.getByRole('button', { name: 'Start cooking' })).toBeInTheDocument();
  });
});

describe('cooking a recipe', () => {
  it('shows only the ingredients the current step names', () => {
    render({ emphasis: 'cook', currentStep: 0 });

    expect(ingredients().getByText(/butter/)).toBeInTheDocument();
    // Flour belongs to the next step, so it is not in the strip yet.
    expect(ingredients().queryByText(/sifted/)).not.toBeInTheDocument();
  });

  it('follows the step being cooked', () => {
    render({ emphasis: 'cook', currentStep: 1 });

    expect(ingredients().getByText(/sifted/)).toBeInTheDocument();
  });

  it('keeps every step on screen, so the last one can still be glanced at', () => {
    render({ emphasis: 'cook', currentStep: 1 });

    expect(steps().getByText(/Melt/)).toBeInTheDocument();
    expect(steps().getByText(/Stir in/)).toBeInTheDocument();
  });

  it('marks which step is current, for a screen reader as well as the eye', () => {
    render({ emphasis: 'cook', currentStep: 1 });

    const current = screen.getByRole('button', { current: 'step' });

    expect(current).toHaveTextContent('Step 2');
  });

  it('keeps the servings control, because one more person arrives mid-cook', () => {
    render({ emphasis: 'cook' });

    expect(screen.getByRole('spinbutton', { name: 'Servings' })).toBeInTheDocument();
  });
});
