/** A run of text, and whether it is what the search matched. */
export interface Stretch {
  readonly text: string;
  readonly matched: boolean;
}

/** The shortest word worth marking: one letter would light up half a title. */
const shortestWord = 2;

/** One character compared as a search does (case and accents ignored: "creme" finds "Crème"), one for one so folded positions match the screen. */
const foldChar = (character: string): string =>
  character.toLocaleLowerCase().normalize('NFD').charAt(0);

const fold = (text: string): string => [...text].map(foldChar).join('');

/** A folded query word plus its German-keyboard spelling ("kaesekuchen" also reads "kasekuchen", what "Käsekuchen" folds to); search reads both, so marks do too. */
const spellings = (word: string): string[] => {
  const plain = word.replaceAll('ae', 'a').replaceAll('oe', 'o').replaceAll('ue', 'u');

  return plain === word ? [word] : [word, plain];
};

/** Splits text into what the typed words matched and what they did not, for titles and match reasons; unmatched text comes back whole. */
export function highlightMatches(text: string, query: string): readonly Stretch[] {
  const characters = [...text];
  const folded = fold(text);
  const marked = new Array<boolean>(characters.length).fill(false);

  for (const word of fold(query).split(/\s+/).flatMap(spellings)) {
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
