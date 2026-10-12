import { foldedWords } from '../foldText';

/**
 * Words that say which kind of a thing to buy ("Spargel, grün", "Kokosmilch, light"), folded the ä → a way.
 * They belong in the name, not the note: the shopping list and nutrition never read the note. Mirrors the
 * qualifiers of the server's `Domain/Ingredients/IngredientWords`.
 */
const declined = (...stems: string[]) =>
  stems.flatMap((stem) => ['', 'e', 'er', 'es', 'en', 'em'].map((ending) => stem + ending));

const forms = new Set([
  ...declined(
    'grun',
    'weiss',
    'rot',
    'gelb',
    'schwarz',
    'violett',
    'braun',
    'fettarm',
    'fettreduziert',
    'mager',
    'halbfett',
    'vollfett',
    'zuckerfrei',
    'ungesusst',
    'gesusst',
    'laktosefrei',
    'gesalzen',
    'ungesalzen',
    'tiefgekuhlt',
    'tiefgefroren',
    'gefroren',
    'getrocknet',
    'gerauchert',
    'gemahlen',
    'ganz',
    'roh',
    'gekocht',
    'suss',
    'scharf',
    'mild',
    'trocken'
  ),
  ...['green', 'white', 'red', 'yellow', 'black', 'lila', 'purple', 'brown', 'light', 'lite'],
  ...[
    'low fat',
    'lowfat',
    'reduced fat',
    'skimmed',
    'skim',
    'full fat',
    'sugar free',
    'unsweetened'
  ],
  ...['sweetened', 'lactose free', 'salted', 'unsalted', 'vollkorn', 'whole wheat', 'whole grain'],
  ...[
    'wholegrain',
    'wholemeal',
    'tk',
    'frozen',
    'dried',
    'smoked',
    'aus der dose',
    'dose',
    'dosen'
  ],
  ...['konserve', 'canned', 'tinned', 'ground', 'whole', 'raw', 'cooked', 'sweet', 'spicy', 'hot']
]);

/** The longest form, in words: "aus der dose". */
const longest = 3;

/** Whether every word of the text names a kind ("grün", "light", "aus der Dose") and none a preparation. */
export function namesAKind(text: string): boolean {
  const words = foldedWords(text);

  for (let at = 0; at < words.length;) {
    const length = [3, 2, 1].find(
      (size) => size <= longest && forms.has(words.slice(at, at + size).join(' '))
    );

    if (!length) {
      return false;
    }

    at += length;
  }

  return words.length > 0;
}
