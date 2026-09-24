/** A run of text, and whether it is what the search matched. */
export interface Stretch {
  readonly text: string;
  readonly matched: boolean;
}

/** The shortest word worth marking: one letter would light up half a title. */
const shortestWord = 2;

/**
 * One character, compared the way a search compares: case and accents
 * ignored, so "creme" finds "Crème". Character for character, so a position
 * in the folded text is the same position in what is on screen.
 */
const foldChar = (character: string): string =>
  character.toLocaleLowerCase().normalize('NFD').charAt(0);

const fold = (text: string): string => [...text].map(foldChar).join('');

/**
 * Splits text into what the typed words matched and what they did not.
 *
 * For the words a person reads a result by — its title and the line saying
 * why it is there — so the reason a recipe was found can be seen, not only
 * read. Where nothing matches, the text comes back whole.
 */
export function highlightMatches(text: string, query: string): readonly Stretch[] {
  const characters = [...text];
  const folded = fold(text);
  const marked = new Array<boolean>(characters.length).fill(false);

  for (const word of fold(query).split(/\s+/)) {
    if (word.length < shortestWord) {
      continue;
    }

    for (let at = folded.indexOf(word); at !== -1; at = folded.indexOf(word, at + 1)) {
      marked.fill(true, at, at + word.length);
    }
  }

  const stretches: Stretch[] = [];

  characters.forEach((character, index) => {
    const last = stretches.at(-1);

    if (last && last.matched === marked[index]) {
      stretches[stretches.length - 1] = { text: last.text + character, matched: last.matched };
    } else {
      stretches.push({ text: character, matched: marked[index]! });
    }
  });

  return stretches;
}
