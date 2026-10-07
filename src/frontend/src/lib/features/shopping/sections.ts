import { m } from '$shell/i18n';

import type { components } from '$api/generated/schema';

/**
 * The sections in the order a shop is walked, from the contract so it can't disagree with the
 * backend.
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

export const nameOf = (section: Section): string => (names[section] ?? names.other)();
