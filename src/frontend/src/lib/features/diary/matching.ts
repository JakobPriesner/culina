import { foldedWords } from '$features/recipes/foldText';

/**
 * How well a name fits what was typed, the way the server's food search reads it: every typed word must start a word
 * of the name, or sit inside one (a compound: "milch" in "Vollmilch"), in any order. Lower is better; null is no fit.
 * For the short lists the sheet filters itself (what was eaten before, the household's own foods), so no typo rule.
 */
export function fit(name: string, query: string): number | null {
  const wanted = foldedWords(query);
  const words = foldedWords(name);

  if (wanted.length === 0) return null;

  let worst = 0;

  for (const typed of wanted) {
    const starts = words.some((word) => word.startsWith(typed));
    const inside = typed.length >= 3 && words.some((word) => word.includes(typed));

    if (!starts && !inside) return null;

    worst = Math.max(worst, starts ? 0 : 1);
  }

  // The name's own first word typed first reads as what was meant: "Milch" before "Buttermilch".
  return worst * 2 + (words[0]?.startsWith(wanted[0] ?? '') ? 0 : 1);
}

/** What fits, best first, then shorter names first; ties keep their order. */
export function fitting<T>(items: readonly T[], nameOf: (item: T) => string, query: string): T[] {
  return items
    .map((item, index) => ({ item, index, rank: fit(nameOf(item), query) }))
    .filter((one): one is { item: T; index: number; rank: number } => one.rank !== null)
    .sort(
      (a, b) =>
        a.rank - b.rank || nameOf(a.item).length - nameOf(b.item).length || a.index - b.index
    )
    .map((one) => one.item);
}
