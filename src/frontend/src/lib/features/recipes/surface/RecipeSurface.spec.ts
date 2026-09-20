import { screen, within } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
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
  yieldLabel: null,
  prepMinutes: 10,
  cookMinutes: 15,
  totalMinutes: 25,
  imageId: null,
  tags: ['Weeknight'],
  sourceUrl: null,
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
      title: null,
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
      title: null,
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

    expect(screen.getByText(/The times are for/)).toBeInTheDocument();
  });

  it('says nothing about times at the recipe’s own yield', () => {
    render();

    expect(screen.queryByText(/The times are for/)).not.toBeInTheDocument();
  });

  it('numbers a step that has no name of its own', () => {
    render();

    expect(steps().getByText('Step 1')).toBeInTheDocument();
  });

  it('calls a step what the recipe calls it, instead of numbering it', () => {
    // The whole point: in a layered recipe, "Step 2" is the least useful thing
    // that could be written above the sentence.
    render({
      recipe: {
        ...recipe,
        steps: [{ ...recipe.steps[0]!, title: 'Prepare the base' }, recipe.steps[1]!]
      }
    });

    expect(steps().getByText('Prepare the base')).toBeInTheDocument();
    expect(steps().queryByText('Step 1')).not.toBeInTheDocument();
  });

  it('says what the recipe makes in the recipe’s own word', () => {
    render({ recipe: { ...recipe, yieldAmount: 1, yieldLabel: 'Cake' }, servings: 1 });

    // The stepper is renamed too, so the control and the wording agree.
    expect(screen.getByRole('spinbutton', { name: 'Cake' })).toBeInTheDocument();
  });

  it('offers to start cooking when the page says cooking is on offer', () => {
    render({ onstartcooking: () => {} });

    expect(screen.getByRole('button', { name: 'Start cooking' })).toBeInTheDocument();
  });

  it('parks nothing at the bottom of the screen when there is no cooking to start', () => {
    // What somebody following a share link gets: they cannot cook a recipe
    // that is not theirs, and a bar floating over the last step with nothing
    // on it is worse than no bar.
    const { container } = render();

    expect(screen.queryByRole('button', { name: 'Start cooking' })).not.toBeInTheDocument();
    expect(container.querySelector('.foot')).toBeNull();
  });

  it('keeps the bottom of the screen for cooking alone', () => {
    // The supporting actions moved to the title row, so offering one of them
    // is not a reason to float anything over the recipe.
    const { container } = render({ onshare: () => {} });

    expect(container.querySelector('.foot')).toBeNull();
  });

  it('offers the shopping list beside the title, without a menu to open first', () => {
    render({ onaddtolist: () => {} });

    // The weekly loop — read a recipe, put it on the list — and a loop that
    // runs twice a week does not belong behind a menu.
    expect(screen.getByRole('button', { name: 'Add to the shopping list' })).toBeInTheDocument();
  });

  it('keeps the occasional actions in one menu, closed until it is asked for', () => {
    render({ editable: true, onshare: () => {}, onaddtocookbook: () => {} });

    // One control on the page, and nothing behind it reachable until it is
    // pressed.
    expect(screen.getByRole('button', { name: 'More actions' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Share' })).not.toBeInTheDocument();

    // What is in it, with the panel's own hiding set aside: jsdom implements
    // neither `showPopover` nor the declarative invocation, so the menu cannot
    // be opened by pressing its trigger here. The end-to-end suite is where a
    // real browser presses it.
    const hidden = { hidden: true } as const;

    expect(screen.getByRole('button', { name: 'Add to cookbook', ...hidden })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Share', ...hidden })).toBeInTheDocument();

    // A link, not a button: a recipe you are about to rewrite is one people
    // open in a second tab beside the one they are reading.
    expect(screen.getByRole('link', { name: 'Edit', ...hidden })).toHaveAttribute(
      'href',
      '/recipes/r1/edit'
    );
  });

  it('says nothing about editing when there is nowhere to edit', () => {
    render();

    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'More actions' })).not.toBeInTheDocument();
  });
});

describe('what a step needs', () => {
  it('lists it under the step while reading, so it can be got out first', () => {
    render();

    const [first] = steps().getAllByText('For this step');

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

    expect(steps().queryByText('For this step')).not.toBeInTheDocument();
  });
});

describe('how the ingredients are arranged', () => {
  // The choice is remembered on the device, so one test's click would
  // otherwise be the next test's starting position.
  afterEach(() => localStorage.clear());

  const chooseByStep = async () => {
    await userEvent.click(screen.getByRole('button', { name: 'By step' }));
  };

  it('puts them all in one place to begin with', () => {
    render();

    expect(ingredients().getByText('300 g')).toBeInTheDocument();
  });

  it('adds the same thing up wherever the recipe asked for it', () => {
    render({
      recipe: {
        ...recipe,
        groups: [
          {
            id: 'g1',
            name: null,
            ingredients: [
              { id: butter, quantity: { value: 200, unit: 'g' }, name: 'butter', note: null },
              { id: 'i-butter-2', quantity: { value: 50, unit: 'g' }, name: 'butter', note: null }
            ]
          }
        ]
      }
    });

    // One block of butter, because that is what the shop sells and what the
    // cook has to weigh.
    expect(ingredients().getByText('250 g')).toBeInTheDocument();
    expect(ingredients().getAllByText(/butter/)).toHaveLength(1);
  });

  it('moves each step’s ingredients beside the step when asked', async () => {
    render();
    await chooseByStep();

    // By its number, not its position: the ingredients beside a step are list
    // items of their own now, so counting them is counting the wrong thing.
    const stepTwo = steps().getByText('Step 2').closest('li')!;

    // The note is what tells the two apart: the gathering line under a step
    // never carried one, and the list in the column does.
    expect(within(stepTwo).getByText(/sifted/)).toBeInTheDocument();
    expect(ingredients().queryByText('300 g')).not.toBeInTheDocument();
  });

  it('drops the step’s own gathering line, which the column has become', async () => {
    render();
    await chooseByStep();

    expect(steps().queryByText('For this step')).not.toBeInTheDocument();
  });

  it('keeps an ingredient no step asks for, rather than losing it', async () => {
    render({
      recipe: {
        ...recipe,
        steps: recipe.steps.map((step) => ({
          ...step,
          uses: step.uses.filter((id) => id !== salt)
        }))
      }
    });
    await chooseByStep();

    // Nothing else on the page would mention it, and an ingredient that
    // disappears because nobody wrote it into a sentence is a recipe the app
    // has quietly changed.
    expect(ingredients().getByText('salt')).toBeInTheDocument();
    expect(ingredients().getByText('Not tied to a step')).toBeInTheDocument();
  });

  it('remembers the arrangement, because it is how this person reads', async () => {
    const first = render();

    await chooseByStep();
    first.unmount();

    render();

    expect(screen.getByRole('button', { name: 'By step' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('offers no choice while cooking, where one step is the whole arrangement', () => {
    render({ emphasis: 'cook' });

    expect(screen.queryByRole('button', { name: 'By step' })).not.toBeInTheDocument();
  });

  it('cooks the same way whichever arrangement was chosen', async () => {
    const { rerender } = render();

    await chooseByStep();
    await rerender({ recipe, servings: 2, emphasis: 'cook', currentStep: 0 });

    // The panel has contracted to the current step, exactly as it does for a
    // reader who never touched the switch.
    expect(ingredients().getByText(/butter/)).toBeInTheDocument();
    expect(ingredients().queryByText(/sifted/)).not.toBeInTheDocument();
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
    expect(steps().queryByText('For this step')).not.toBeInTheDocument();
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
