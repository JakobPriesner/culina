import { toSegments } from '$features/recipes/editor/mentions';

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
  // Whichever list the steps are about to sit beside: the new one when it was
  // accepted, the one already there when it was not. Worked out before the
  // patch because the steps are linked against it.
  const ingredients = accepted.ingredients ? toIngredients(draft) : currentIngredients(current);

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
      ? { groups: [{ id: current.groups[0]?.id ?? null, name: null, ingredients }] }
      : {}),
    ...(accepted.steps ? { steps: toSteps(draft, ingredients) } : {})
  };
}

const currentIngredients = (recipe: Recipe): Ingredient[] =>
  recipe.groups.flatMap((group) => [...group.ingredients]);

/**
 * The draft's lines, flattened into the one group the editor shows.
 *
 * Groups are dropped rather than created: the editor edits a single list, and a
 * heading the person cannot see or move is a heading they cannot delete either.
 * What the model split into "for the sauce" and "for the topping" arrives in
 * that order, which is most of what the split was saying.
 *
 * Each line is given an id here rather than left blank for the server to fill
 * in, which is the difference between an improved recipe that keeps Culina's
 * second promise and one that quietly breaks it. A step refers to an ingredient
 * by id, so a list with no ids yet is a list no step can name — and accepting
 * new ingredients and new steps together, which is the ordinary thing to do,
 * would produce a recipe whose steps had lost every scalable amount. The server
 * takes the id it is given (`id ?? CulinaId.New()`), so minting it one step
 * earlier costs nothing and keeps the links.
 */
function toIngredients(draft: Draft): Ingredient[] {
  return draft.groups.flatMap((group) =>
    group.ingredients.map((line) => ({
      id: crypto.randomUUID(),
      quantity: { value: line.quantity ?? null, unit: line.unit ?? null },
      name: line.name,
      note: line.note ?? null
    }))
  );
}

/**
 * The draft's steps, with their ingredient references put back.
 *
 * A model answers in sentences, so a step arrives as words. Culina's second
 * promise is that a step knows which ingredients it uses — that is what lets an
 * amount inside a sentence scale — and a set of steps that had lost every link
 * would be a worse recipe than the one being improved.
 *
 * So the names are found again here, deterministically, against the list that
 * is about to exist. Longest first, and only on a word boundary, so "oil" does
 * not match inside "olive oil" or inside "boiling".
 */
function toSteps(draft: Draft, ingredients: readonly Ingredient[]): Step[] {
  return draft.steps.map((step) => ({
    id: null,
    title: step.title ?? null,
    segments: toSegments(withMentions(step.text, ingredients), ingredients),
    durationSeconds: step.durationSeconds ?? null,
    uses: []
  }));
}

/** The marker `toSegments` reads a mention from. */
const marker = '@';

/**
 * Marks each ingredient name found in a sentence, for `toSegments` to read.
 *
 * Reusing that function rather than matching here a second time: it already
 * knows how a mention is spelled and which name wins when two overlap, and two
 * answers to that question is one more than the app can have.
 */
export function withMentions(text: string, ingredients: readonly Ingredient[]): string {
  const names = ingredients
    .map((one) => one.name)
    .filter((name) => name.length > 1)
    .sort((a, b) => b.length - a.length);

  let marked = '';
  let index = 0;

  outer: while (index < text.length) {
    if (isWordStart(text, index)) {
      for (const name of names) {
        if (matchesAt(text, index, name) && isWordEnd(text, index + name.length)) {
          marked += marker + text.slice(index, index + name.length);
          index += name.length;

          continue outer;
        }
      }
    }

    marked += text[index];
    index += 1;
  }

  return marked;
}

const isLetter = (character: string | undefined): boolean =>
  character !== undefined && /[\p{L}\p{N}]/u.test(character);

const isWordStart = (text: string, index: number): boolean => !isLetter(text[index - 1]);

const isWordEnd = (text: string, index: number): boolean => !isLetter(text[index]);

const matchesAt = (text: string, index: number, name: string): boolean =>
  text.slice(index, index + name.length).toLocaleLowerCase() === name.toLocaleLowerCase();
