import type { Ingredient, Recipe, Step } from '$features/recipes/types';
import type { components } from '$api/generated/schema';

type DraftWire = components['schemas']['RecipesDraftsResponse'];

export type Draft = DraftWire;

/**
 * Which parts of a draft somebody has accepted.
 *
 * Six switches rather than one per ingredient and one per step, and that is a
 * decision rather than a shortcut. A recipe whose steps came from the assistant
 * and whose ingredients did not is a recipe whose steps name things that are no
 * longer in it — so the list is the smallest unit that stays coherent. Within a
 * list, correcting one line is what the editor underneath is for.
 */
export interface Accepted {
  title: boolean;
  description: boolean;
  yield: boolean;
  times: boolean;
  ingredients: boolean;
  steps: boolean;
}

/** Nothing accepted. The state the review opens in. */
export const acceptNothing = (): Accepted => ({
  title: false,
  description: false,
  yield: false,
  times: false,
  ingredients: false,
  steps: false
});

/** Everything the draft actually offers. */
export const acceptEverything = (draft: Draft): Accepted => ({
  title: offers(draft).title,
  description: offers(draft).description,
  yield: offers(draft).yield,
  times: offers(draft).times,
  ingredients: offers(draft).ingredients,
  steps: offers(draft).steps
});

/**
 * Which parts the draft has anything to say about.
 *
 * A draft that did not mention the cooking time must not offer to replace the
 * time with nothing — an accepted blank is a deletion nobody asked for.
 */
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

/** Whether anything at all has been accepted. */
export const anyAccepted = (accepted: Accepted): boolean => Object.values(accepted).some(Boolean);

/**
 * Turns the accepted parts into the patch the editor applies.
 *
 * One patch, so accepting six things is one autosave and one version bump
 * rather than six of each.
 */
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

/**
 * The draft's lines, flattened into the one group the editor shows.
 *
 * Groups are dropped rather than created: the editor edits a single list, and a
 * heading the person cannot see or move is a heading they cannot delete either.
 * What the model split into "for the sauce" and "for the topping" arrives in
 * that order, which is most of what the split was saying.
 */
function toIngredients(draft: Draft): Ingredient[] {
  return draft.groups.flatMap((group) =>
    group.ingredients.map((line) => ({
      // No id: these are new lines, and the server allocates them on save.
      id: '',
      quantity: { value: line.quantity ?? null, unit: line.unit ?? null },
      name: line.name,
      note: line.note ?? null
    }))
  );
}

/**
 * The draft's steps, as words.
 *
 * Not linked to the ingredients they name, although it is tempting and
 * although the names are right there. The paste-import path next door says why
 * it does not do this either: "guessing which ones were meant is the silent
 * linking this editor deliberately stopped doing." A link that is wrong shows a
 * scaled amount inside a sentence that was never about that ingredient, which
 * is worse than no link at all — and the person is about to read every one of
 * these steps anyway, where an `@` costs them one keystroke.
 *
 * The cost is real: a recipe whose steps are replaced loses the links its old
 * steps had. That is the same trade a pasted recipe makes, and it is the
 * editor's rule rather than this feature's to change.
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
