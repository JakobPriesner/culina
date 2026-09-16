import { screen, within } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import RecipeSurface from './RecipeSurface.svelte';
import type { Recipe } from '../types';
import { renderWithProviders } from '$lib/test/render';

const butter = 'i-butter';
const flour = 'i-flour';
const salt = 'i-salt';

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
        { id: flour, quantity: { value: 300, unit: 'g' }, name: 'flour', note: 'sifted' },
        { id: salt, quantity: { value: null, unit: null }, name: 'salt', note: null }
      ]
    }
  ],
  steps: [
    {
      id: 's1',
      durationSeconds: null,
      // Butter is named in the sentence; salt is only ever needed, which is the
      // half a step's words cannot say.
      uses: [butter, salt],
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
      uses: [flour],
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

  it('offers the way back into the editor, as a real link', () => {
    render({ editable: true });

    // A link, not a button: a recipe you are about to rewrite is one people
    // open in a second tab beside the one they are reading.
    expect(screen.getByRole('link', { name: 'Edit' })).toHaveAttribute('href', '/recipes/r1/edit');
  });

  it('says nothing about editing when there is nowhere to edit', () => {
    render();

    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
  });
});

describe('what a step needs', () => {
  it('lists it under the step while reading, so it can be got out first', () => {
    render();

    const [first] = steps().getAllByText('Get out');

    expect(first).toBeInTheDocument();
  });

  it('names what the sentence leaves out', () => {
    render();

    // "Melt butter in the pan" needs salt and does not say so. The list under
    // it is the only place a reader would ever find that out.
    expect(steps().getByText('salt')).toBeInTheDocument();
  });

  it('scales with everything else, from the one number', () => {
    render({ servings: 4 });

    const stepTwo = steps().getAllByRole('listitem')[1]!;

    // Normalised by the query: the amount itself is joined with a
    // non-breaking space, so it never wraps away from its unit.
    expect(within(stepTwo).getByText('600 g')).toBeInTheDocument();
  });

  it('says nothing for a step that needs nothing', () => {
    render({
      recipe: {
        ...recipe,
        steps: [
          { id: 's1', uses: [], durationSeconds: null, segments: [{ kind: 'text', text: 'Rest.' }] }
        ]
      }
    });

    expect(steps().queryByText('Get out')).not.toBeInTheDocument();
  });
});

describe('cooking a recipe', () => {
  it('shows only the ingredients the current step needs', () => {
    render({ emphasis: 'cook', currentStep: 0 });

    expect(ingredients().getByText(/butter/)).toBeInTheDocument();
    // Flour belongs to the next step, so it is not in the strip yet.
    expect(ingredients().queryByText(/sifted/)).not.toBeInTheDocument();
  });

  it('shows what the step needs but never says', () => {
    render({ emphasis: 'cook', currentStep: 0 });

    // The half the sentence cannot carry: step one says "melt butter in the
    // pan" and says nothing at all about salt, which you still need in hand.
    expect(ingredients().getByText('salt')).toBeInTheDocument();
  });

  it('drops the step’s own list, because the panel has become it', () => {
    render({ emphasis: 'cook', currentStep: 0 });

    // Saying it twice on a screen read from across the kitchen is worse than
    // saying it once.
    expect(steps().queryByText('Get out')).not.toBeInTheDocument();
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

  describe('following the step being cooked', () => {
    // jsdom lays nothing out and scrolls nothing, so the method the page calls
    // does not exist on an element here. What is worth proving is which
    // element is asked to come into view, and when.
    const scrollIntoView = vi.fn();

    const withScrolling = (props: Record<string, unknown>) => {
      Element.prototype.scrollIntoView = scrollIntoView;

      return render(props);
    };

    afterEach(() => {
      scrollIntoView.mockClear();
      delete (Element.prototype as Partial<Element>).scrollIntoView;
    });

    it('brings the new step onto the screen', async () => {
      const { rerender } = withScrolling({ emphasis: 'cook', currentStep: 0 });

      await rerender({ recipe, servings: 2, emphasis: 'cook', currentStep: 1 });
      await vi.waitFor(() => expect(scrollIntoView).toHaveBeenCalled());

      expect(scrollIntoView.mock.contexts[0]).toHaveTextContent('Step 2');
    });

    it('leaves the page where the reader put it while reading', async () => {
      const { rerender } = withScrolling({ emphasis: 'read', currentStep: 0 });

      await rerender({ recipe, servings: 2, emphasis: 'read', currentStep: 1 });

      expect(scrollIntoView).not.toHaveBeenCalled();
    });

    it('does not move the page the moment it opens', async () => {
      withScrolling({ emphasis: 'cook', currentStep: 1 });
      await Promise.resolve();

      // Arriving mid-recipe is the page loading, not the cook moving: a page
      // that scrolls itself as it appears has taken them somewhere they did
      // not ask to go.
      expect(scrollIntoView).not.toHaveBeenCalled();
    });
  });

  it('keeps the servings control, because one more person arrives mid-cook', () => {
    render({ emphasis: 'cook' });

    expect(screen.getByRole('spinbutton', { name: 'Servings' })).toBeInTheDocument();
  });

  it('takes the editor away, hands being full', () => {
    render({ emphasis: 'cook', editable: true });

    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
  });
});
