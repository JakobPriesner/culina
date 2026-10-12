/**
 * Counted foods in both numbers, so a scaled "1 Zwiebel" reads "2 Zwiebeln". A copy of the server's
 * `Domain/Ingredients/IngredientNouns`, which a backend test holds this file to: change both.
 * Listed rather than generated (Apfel, Äpfel; Ei, Eier); a word not listed is never changed, and a compound
 * takes the forms of its last part (Knoblauch·zehe).
 */
type Gender = 'm' | 'f' | 'n';

export const germanNouns: readonly (readonly [one: string, many: string, gender: Gender])[] = [
  ['Apfel', 'Äpfel', 'm'],
  ['Aprikose', 'Aprikosen', 'f'],
  ['Artischocke', 'Artischocken', 'f'],
  ['Aubergine', 'Auberginen', 'f'],
  ['Avocado', 'Avocados', 'f'],
  ['Banane', 'Bananen', 'f'],
  ['Beere', 'Beeren', 'f'],
  ['Birne', 'Birnen', 'f'],
  ['Blatt', 'Blätter', 'n'],
  ['Brezel', 'Brezeln', 'f'],
  ['Brot', 'Brote', 'n'],
  ['Brust', 'Brüste', 'f'],
  ['Champignon', 'Champignons', 'm'],
  ['Dattel', 'Datteln', 'f'],
  ['Ei', 'Eier', 'n'],
  ['Eigelb', 'Eigelbe', 'n'],
  ['Eiweiß', 'Eiweiße', 'n'],
  ['Feige', 'Feigen', 'f'],
  ['Filet', 'Filets', 'n'],
  ['Frikadelle', 'Frikadellen', 'f'],
  ['Garnele', 'Garnelen', 'f'],
  ['Gurke', 'Gurken', 'f'],
  ['Kapsel', 'Kapseln', 'f'],
  ['Karotte', 'Karotten', 'f'],
  ['Kartoffel', 'Kartoffeln', 'f'],
  ['Keule', 'Keulen', 'f'],
  ['Kirsche', 'Kirschen', 'f'],
  ['Kiwi', 'Kiwis', 'f'],
  ['Kloß', 'Klöße', 'm'],
  ['Knolle', 'Knollen', 'f'],
  ['Limette', 'Limetten', 'f'],
  ['Mandarine', 'Mandarinen', 'f'],
  ['Mandel', 'Mandeln', 'f'],
  ['Mango', 'Mangos', 'f'],
  ['Melone', 'Melonen', 'f'],
  ['Möhre', 'Möhren', 'f'],
  ['Muschel', 'Muscheln', 'f'],
  ['Nektarine', 'Nektarinen', 'f'],
  ['Nelke', 'Nelken', 'f'],
  ['Nuss', 'Nüsse', 'f'],
  ['Olive', 'Oliven', 'f'],
  ['Orange', 'Orangen', 'f'],
  ['Pfirsich', 'Pfirsiche', 'm'],
  ['Pflaume', 'Pflaumen', 'f'],
  ['Pilz', 'Pilze', 'm'],
  ['Rübe', 'Rüben', 'f'],
  ['Sardelle', 'Sardellen', 'f'],
  ['Schalotte', 'Schalotten', 'f'],
  ['Scheibe', 'Scheiben', 'f'],
  ['Schote', 'Schoten', 'f'],
  ['Semmel', 'Semmeln', 'f'],
  ['Spieß', 'Spieße', 'm'],
  ['Stange', 'Stangen', 'f'],
  ['Steak', 'Steaks', 'n'],
  ['Tomate', 'Tomaten', 'f'],
  ['Tortilla', 'Tortillas', 'f'],
  ['Traube', 'Trauben', 'f'],
  ['Wrap', 'Wraps', 'm'],
  ['Wurst', 'Würste', 'f'],
  ['Zehe', 'Zehen', 'f'],
  ['Zitrone', 'Zitronen', 'f'],
  ['Zwiebel', 'Zwiebeln', 'f']
];

export const englishNouns: readonly (readonly [one: string, many: string])[] = [
  ['apple', 'apples'],
  ['apricot', 'apricots'],
  ['aubergine', 'aubergines'],
  ['avocado', 'avocados'],
  ['banana', 'bananas'],
  ['berry', 'berries'],
  ['breast', 'breasts'],
  ['carrot', 'carrots'],
  ['cherry', 'cherries'],
  ['clove', 'cloves'],
  ['courgette', 'courgettes'],
  ['cucumber', 'cucumbers'],
  ['date', 'dates'],
  ['egg', 'eggs'],
  ['fig', 'figs'],
  ['fillet', 'fillets'],
  ['leaf', 'leaves'],
  ['leek', 'leeks'],
  ['lemon', 'lemons'],
  ['lime', 'limes'],
  ['mango', 'mangoes'],
  ['mushroom', 'mushrooms'],
  ['nectarine', 'nectarines'],
  ['olive', 'olives'],
  ['onion', 'onions'],
  ['orange', 'oranges'],
  ['peach', 'peaches'],
  ['pear', 'pears'],
  ['pepper', 'peppers'],
  ['plum', 'plums'],
  ['potato', 'potatoes'],
  ['prawn', 'prawns'],
  ['radish', 'radishes'],
  ['sausage', 'sausages'],
  ['scallion', 'scallions'],
  ['shallot', 'shallots'],
  ['steak', 'steaks'],
  ['thigh', 'thighs'],
  ['tomato', 'tomatoes'],
  ['tortilla', 'tortillas']
];

interface Noun {
  readonly one: string;
  readonly many: string;
  readonly gender: Gender | null;
}

/** The shortest listed form that may end a compound ("Zehe" ends Knoblauchzehe, "Ei" ends nothing), and the shortest front. */
const shortestCompoundForm = 4;
const shortestCompoundFront = 3;

const german: readonly Noun[] = germanNouns.map(([one, many, gender]) => ({ one, many, gender }));
const english: readonly Noun[] = englishNouns.map(([one, many]) => ({ one, many, gender: null }));

const articles = new Set(['die', 'der', 'das', 'des', 'eine', 'einer', 'eines', 'jede', 'jeder']);

/**
 * The name with its food in the number a bare count asks for: "Zwiebel" for 2 is "Zwiebeln", "große Äpfel" for 1
 * is "großer Apfel". After a unit ("500 g Tomaten", "2 Scheiben Brot") or without an amount, the name as written.
 */
export function forAmount(
  name: string,
  amount: number | null,
  hasUnit: boolean,
  language: 'de' | 'en' | null
): string {
  if (amount === null || hasUnit) {
    return name;
  }

  const many = amount > 1;
  const words = name.split(' ');

  // The food is the last listed word: "Zwiebel, rot" and "rote Zwiebel" alike.
  for (let at = words.length - 1; at >= 0; at--) {
    const [, word = '', trailing = ''] = /^(.*?)([^\p{L}]*)$/u.exec(words[at]!) ?? [];
    const found = find(word, language);

    if (!found) {
      continue;
    }

    if (found.isMany === many) {
      return name;
    }

    words[at] = respell(word, found.front, many ? found.noun.many : found.noun.one) + trailing;

    if (found.noun.gender) {
      decline(words, at, found.noun.gender, many);
    }

    return words.join(' ');
  }

  return name;
}

function find(word: string, language: 'de' | 'en' | null) {
  const table = language === 'de' ? german : language === 'en' ? english : [...german, ...english];
  const lower = word.toLowerCase();

  // Longest form first, so "Knoblauchzehen" reads as Zehen.
  return table
    .flatMap((noun) => [
      { noun, form: noun.many, isMany: true },
      { noun, form: noun.one, isMany: false }
    ])
    .sort((a, b) => b.form.length - a.form.length)
    .filter(({ form }) => ends(lower, form.toLowerCase()))
    .map(({ noun, form, isMany }) => ({
      noun,
      isMany,
      front: word.slice(0, word.length - form.length)
    }))[0];
}

const ends = (word: string, form: string) =>
  word === form ||
  (form.length >= shortestCompoundForm &&
    word.length - form.length >= shortestCompoundFront &&
    word.endsWith(form));

// The listed form in the written word's case: "Zwiebel" stays capitalised, "Knoblauch·zehe" lower.
const respell = (word: string, front: string, form: string) =>
  front.length === 0
    ? (word[0] === word[0]!.toUpperCase() ? form[0]!.toUpperCase() : form[0]!.toLowerCase()) +
      form.slice(1)
    : front + form.toLowerCase();

// Only the lower-case words right before the food, as German writes an adjective: "große", "rote".
function decline(words: string[], food: number, gender: Gender, many: boolean) {
  for (let at = food - 1; at >= 0 && isAdjective(words[at]!); at--) {
    const word = words[at]!;

    words[at] = many
      ? word.endsWith('er') || word.endsWith('es')
        ? word.slice(0, -1)
        : word
      : word.endsWith('e') && gender !== 'f'
        ? word + (gender === 'm' ? 'r' : 's')
        : word;
  }
}

const isAdjective = (word: string) =>
  word.length >= 3 &&
  word[0] === word[0]!.toLowerCase() &&
  /^\p{L}+$/u.test(word) &&
  !articles.has(word) &&
  (word.endsWith('e') || word.endsWith('er') || word.endsWith('es'));
