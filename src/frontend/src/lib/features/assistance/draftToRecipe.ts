import type { Ingredient, Recipe, Step } from '$features/recipes/types';
import type { components } from '$api/generated/schema';

type DraftWire = components['schemas']['RecipesDraftsResponse'];

export type Draft = DraftWire;

/**
 * Which parts of a draft are accepted. Six switches, not per line: steps from the assistant
 * over ingredients that weren't would name things no longer in the recipe.
 */
export interface Accepted {
  title: boolean;
  description: boolean;
  yield: boolean;
  times: boolean;
  ingredients: boolean;
  steps: boolean;
}

export const acceptNothing = (): Accepted => ({
  title: false,
  description: false,
  yield: false,
  times: false,
  ingredients: false,
  steps: false
});

export const acceptEverything = (draft: Draft): Accepted => ({
  title: offers(draft).title,
  description: offers(draft).description,
  yield: offers(draft).yield,
  times: offers(draft).times,
  ingredients: offers(draft).ingredients,
  steps: offers(draft).steps
});

/** Which parts the draft says anything about; an accepted blank would delete a value nobody asked to. */
export function offers(draft: Draft): Accepted {
  return {
    title: Boolean(draft.title),
    description: Boolean(draft.description),
    yield: draft.yieldAmount != null || Boolean(draft.yieldLabel),
    times: draft.prepMinutes != null || draft.cookMinutes != null,
    ingredients: draft.groups.some((group) => group.ingredients.length > 0),
    steps: draft.steps.length > 0
  };
}

export const anyAccepted = (accepted: Accepted): boolean => Object.values(accepted).some(Boolean);

/** An empty draft (provider stopped before writing) would show an empty box instead of the reason. */
export const saysAnything = (draft: Draft | null): boolean =>
  draft !== null && anyAccepted(offers(draft));

/** One patch, so accepting six things is one autosave and one version bump. */
export function toPatch(draft: Draft, accepted: Accepted, current: Recipe): Partial<Recipe> {
  return {
    ...(accepted.title && draft.title ? { title: draft.title } : {}),
    ...(accepted.description && draft.description ? { description: draft.description } : {}),
    ...(accepted.yield
      ? {
          yieldAmount: draft.yieldAmount ?? current.yieldAmount,
          yieldLabel: draft.yieldLabel ?? current.yieldLabel
        }
      : {}),
    ...(accepted.times
      ? {
          prepMinutes: draft.prepMinutes ?? current.prepMinutes,
          cookMinutes: draft.cookMinutes ?? current.cookMinutes
        }
      : {}),
    ...(accepted.ingredients
      ? {
          groups: [
            { id: current.groups[0]?.id ?? null, name: null, ingredients: toIngredients(draft) }
          ]
        }
      : {}),
    ...(accepted.steps ? { steps: toSteps(draft) } : {})
  };
}

/** Flattened into the editor's single list; a group heading it can't show or move couldn't be deleted either. */
function toIngredients(draft: Draft): Ingredient[] {
  return draft.groups.flatMap((group) =>
    group.ingredients.map((line) => ({
      // No id: new lines; the server allocates on save.
      id: '',
      quantity: { value: line.quantity ?? null, unit: line.unit ?? null },
      name: line.name,
      note: line.note ?? null
    }))
  );
}

/**
 * Steps as plain words, not linked to the ingredients they name: a wrong link shows a scaled
 * amount in an unrelated sentence, and an `@` costs one keystroke. Same trade as paste-import.
 */
function toSteps(draft: Draft): Step[] {
  return draft.steps.map((step) => ({
    id: null,
    title: step.title ?? null,
    segments: [{ kind: 'text' as const, text: step.text }],
    durationSeconds: step.durationSeconds ?? null,
    uses: []
  }));
}
