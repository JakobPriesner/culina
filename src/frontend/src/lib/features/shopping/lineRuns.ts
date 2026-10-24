import { sectionOrder } from './sections';
import type { SectionGroup, ShoppingItem } from './stores/shopping.svelte';

/*
 * Lines of one thing the server keeps apart, because their units cannot be added ("1 Prise Salz",
 * "1 TL Salz", "Salz"): read together, they are one thing written several ways.
 */

const nameKey = (item: ShoppingItem): string => item.name.trim().toLocaleLowerCase();

/** The lines in their order, each name's lines moved up to its first one. */
export function togetherByName(items: readonly ShoppingItem[]): ShoppingItem[] {
  const names = items.map(nameKey).filter((name, index, all) => all.indexOf(name) === index);

  return names.flatMap((name) => items.filter((item) => nameKey(item) === name));
}

/** The last line of each run of the same name: where a note about the run is said, once. */
export function endsItsName(items: readonly ShoppingItem[], index: number): boolean {
  const next = items[index + 1];

  return next === undefined || nameKey(next) !== nameKey(items[index]!);
}

/**
 * What is still to buy, by section in shop order, same names together; `keep` names ticked lines that
 * stay where they were for a moment, so the list does not move under the thumb that ticked one.
 */
export function groupsToBuy(
  items: readonly ShoppingItem[],
  keep: readonly string[] = []
): SectionGroup[] {
  const remaining = items.filter((item) => !item.isChecked || keep.includes(item.itemId));
  // A section this client does not know (a newer server's) is drawn as "other", so no counted line goes missing.
  const sectionOf = (item: ShoppingItem) =>
    sectionOrder.includes(item.section) ? item.section : 'other';

  return sectionOrder
    .map((section) => ({
      section,
      items: togetherByName(remaining.filter((item) => sectionOf(item) === section))
    }))
    .filter((group) => group.items.length > 0);
}
