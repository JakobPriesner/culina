import type { ShoppingItem } from './stores/shopping.svelte';

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
