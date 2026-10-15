import { linkMentions } from './mentions';
import type { Recipe } from '../types';

/**
 * Takes only the server-assigned version, group, ingredient and step ids from a save, leaving typed edits intact.
 * `sent` is the version the save went out with, so writes made mid-save (e.g. a photo) are kept.
 * `linked` is true when newly id'd lines turned plain "@name" mentions into links, so the draft needs saving again.
 */
export function adoptSaved(
  draft: Recipe,
  saved: Recipe,
  sent: number
): { recipe: Recipe; linked: boolean } {
  // Claimed one by one so same-named lines get distinct ids. Each group draws only from the saved
  // group with its own id: drawing from every group could give one id to two groups, which the save
  // cannot insert twice. Ids the draft already holds are not on offer: a second "Salz" took the
  // first one's id, and every save after was refused as a duplicate.
  const held = new Set(draft.groups.flatMap((group) => group.ingredients.map((one) => one.id)));
  const unclaimed = new Map(
    saved.groups.map((group) => [group.id, group.ingredients.filter((one) => !held.has(one.id))])
  );

  const claim = (groupId: string | null, name: string): string => {
    const pool = unclaimed.get(groupId) ?? [];
    const at = pool.findIndex((one) => one.name.toLowerCase() === name.toLowerCase());

    return at === -1 ? '' : pool.splice(at, 1)[0]!.id;
  };

  // A new group takes the id the server made for the group at its position, unless the draft already holds it.
  const heldGroupIds = new Set(draft.groups.map((group) => group.id));

  const groups = draft.groups.map((group, index) => {
    const savedId = saved.groups[index]?.id ?? null;
    const id = group.id ?? (heldGroupIds.has(savedId) ? null : savedId);

    return {
      ...group,
      id,
      ingredients: group.ingredients.map((one) =>
        one.id ? one : { ...one, id: claim(id, one.name) }
      )
    };
  });
  const ingredients = groups.flatMap((group) => group.ingredients);
  // By position, only onto steps without an id and never an id the draft already holds (a reorder mid-save).
  const heldStepIds = new Set(draft.steps.map((step) => step.id));
  const withIds = draft.steps.map((step, index) => {
    const savedId = saved.steps[index]?.id;

    return step.id || !savedId || heldStepIds.has(savedId) ? step : { ...step, id: savedId };
  });
  const steps = withIds.map((step) => linkMentions(step, ingredients));
  // An adopted step id alone needs no second save; a newly linked mention does.
  const linked = steps.some((step, index) => step !== withIds[index]);

  return {
    recipe: { ...draft, version: saved.version + (draft.version - sent), groups, steps },
    linked
  };
}

/**
 * A line repeating an earlier line's id loses it, so the next save makes it a new line. A journal kept
 * before `adoptSaved` stopped handing out held ids has such twins; reopened as it was, the server refused
 * every save and the step's ingredient list would not render.
 */
export function withDistinctIds(recipe: Recipe): Recipe {
  const seen = new Set<string>();

  return {
    ...recipe,
    groups: recipe.groups.map((group) => ({
      ...group,
      ingredients: group.ingredients.map((one) => {
        if (!one.id || !seen.has(one.id)) {
          seen.add(one.id);

          return one;
        }

        return { ...one, id: '' };
      })
    }))
  };
}
