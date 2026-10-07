import { linkMentions } from './mentions';
import type { Recipe } from '../types';

/**
 * Takes what the server assigned, and nothing else.
 *
 * The draft is otherwise never overwritten from the store — that would delete
 * whatever was typed while the save was in the air — but two things have to
 * come back from it.
 *
 * The **version**, because every write carries the one the author last saw
 * and the server hands out a new one each time. A draft that keeps the
 * version it opened with saves exactly once and then tells the author that
 * somebody else changed their recipe, which is both wrong and alarming.
 * A photo written while the save was in the air moved it on past the answer
 * — see `photoWritten` — and those steps are kept rather than taken back:
 * `sent` is the version this save went out with.
 *
 * The **ingredient ids**, because a line with no id cannot be mentioned in a
 * step. Without them the author would have to reload the page before they
 * could point a sentence at a line they wrote a moment ago. Matched by name
 * and only onto lines that have none yet, so a line added or removed
 * mid-save can at worst miss an id rather than inherit the wrong one; the
 * next save fills it in.
 *
 * A line added from inside a step was mentioned before it had one of those
 * ids, so the step still says "@saffron" in plain words. Once the id is
 * here the mention is linked, and `linked` says the draft has to be saved
 * again: otherwise it would stay plain text until somebody happened to type
 * in that step.
 */
export function adoptSaved(
  draft: Recipe,
  saved: Recipe,
  sent: number
): { recipe: Recipe; linked: boolean } {
  // Taken from the pool as they are used, so two lines of the same name get
  // an id each rather than both getting the first one.
  //
  // Only the first group's, because that is the only one the editor writes
  // to. Drawing from every group let a new "flour" in the first group claim
  // the id of a "flour" in the second, and the same ingredient id in two
  // groups is a primary key the save cannot insert twice.
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
