import { linkMentions } from './mentions';
import type { Recipe } from '../types';

/**
 * Takes only the server-assigned version and ingredient ids from a save, leaving typed edits intact.
 * `sent` is the version the save went out with, so writes made mid-save (e.g. a photo) are kept.
 * `linked` is true when newly id'd lines turned plain "@name" mentions into links, so the draft needs saving again.
 */
export function adoptSaved(
  draft: Recipe,
  saved: Recipe,
  sent: number
): { recipe: Recipe; linked: boolean } {
  // Claimed one by one so same-named lines get distinct ids. First group only: drawing from every
  // group could give one id to two groups, which the save cannot insert twice.
  const unclaimed = [...(saved.groups[0]?.ingredients ?? [])];

  const claim = (name: string): string => {
    const at = unclaimed.findIndex((one) => one.name.toLowerCase() === name.toLowerCase());

    return at === -1 ? '' : unclaimed.splice(at, 1)[0]!.id;
  };

  const groups = draft.groups.map((group) => ({
    ...group,
    ingredients: group.ingredients.map((one) => (one.id ? one : { ...one, id: claim(one.name) }))
  }));
  const ingredients = groups.flatMap((group) => group.ingredients);
  const steps = draft.steps.map((step) => linkMentions(step, ingredients));
  const linked = steps.some((step, index) => step !== draft.steps[index]);

  return {
    recipe: { ...draft, version: saved.version + (draft.version - sent), groups, steps },
    linked
  };
}
