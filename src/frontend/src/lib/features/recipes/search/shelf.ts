import type { CookbookRules } from '$features/cookbooks/types';

import type { Interpretation } from '../types';

/**
 * A search as the cookbook that would ask the same question. A smart cookbook asks only for tags, ingredients
 * and a time limit, so those chips cross over; everything else (diet, cuisine, meal) is named in `behind`, never dropped.
 */
export interface Shelving {
  readonly rules: CookbookRules;
  /** What the search asked that the cookbook cannot, as it was typed. */
  readonly behind: readonly string[];
}

export function shelfFrom(
  interpretation: Interpretation | null,
  tags: readonly { readonly slug: string }[]
): Shelving {
  const chips = interpretation?.chips ?? [];

  const minutes = chips
    .filter((chip) => chip.kind === 'time')
    .map((chip) => Number(chip.value))
    .filter((value) => Number.isFinite(value) && value > 0);

  const ingredients = chips
    .filter((chip) => chip.kind === 'ingredient')
    .map((chip) => (chip.word ?? chip.text).trim())
    .filter((word) => word.length > 0);

  const behind = [
    ...chips
      .filter((chip) => chip.kind !== 'time' && chip.kind !== 'ingredient')
      .map((chip) => chip.text),
    ...(interpretation?.freeText.trim() ? [interpretation.freeText.trim()] : [])
  ];

  return {
    rules: {
      tags: [...new Set(tags.map((tag) => tag.slug))],
      ingredients: [...new Set(ingredients)],
      // Two limits in one query are one limit: the shorter.
      maxMinutes: minutes.length > 0 ? Math.min(...minutes) : null
    },
    behind
  };
}

/** Whether there is anything a cookbook could ask for at all. */
export const shelvable = (shelving: Shelving): boolean =>
  shelving.rules.tags.length > 0 ||
  shelving.rules.ingredients.length > 0 ||
  shelving.rules.maxMinutes !== null;
