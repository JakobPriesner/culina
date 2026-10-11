/** Lower case with umlauts and ß spelled plainly, so "Hartkäse", "HARTKASE" and "hartkase" compare equal. */
export const foldText = (text: string): string =>
  text
    .toLowerCase()
    .replaceAll('ä', 'a')
    .replaceAll('ö', 'o')
    .replaceAll('ü', 'u')
    .replaceAll('ß', 'ss');

/** The folded words of a text, at least `shortest` letters long. */
export const foldedWords = (text: string, shortest = 1): string[] =>
  foldText(text)
    .split(/[^a-z0-9]+/)
    .filter((word) => word.length >= shortest);
