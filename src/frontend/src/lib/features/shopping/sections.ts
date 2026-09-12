import { m } from '$shell/i18n';

import type { components } from '$api/generated/schema';

/**
 * The sections, in the order a shop is walked.
 *
 * That order is the entire value of sections: a list read top to bottom is a
 * route rather than a scavenger hunt. It comes from the contract, so the
 * backend's ordering and this one cannot disagree.
 */
export type Section = components['schemas']['ShoppingItemContract']['section'];

export const sectionOrder: readonly Section[] = [
  'produce',
  'dairy_eggs',
  'meat_fish',
  'bakery',
  'dry_goods',
  'canned_jars',
  'frozen',
  'spices_baking',
  'drinks',
  'household',
  'other'
];

const names: Record<Section, () => string> = {
  produce: m['section.produce'],
  dairy_eggs: m['section.dairy_eggs'],
  meat_fish: m['section.meat_fish'],
  bakery: m['section.bakery'],
  dry_goods: m['section.dry_goods'],
  canned_jars: m['section.canned_jars'],
  frozen: m['section.frozen'],
  spices_baking: m['section.spices_baking'],
  drinks: m['section.drinks'],
  household: m['section.household'],
  other: m['section.other']
};

/** A section's name, falling back rather than rendering a blank heading. */
export const nameOf = (section: Section): string => (names[section] ?? names.other)();
