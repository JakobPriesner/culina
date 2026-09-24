import type { BuiltInUnit } from './units';

/**
 * Folds a typed word to the form the table below is keyed by.
 *
 * German is written with umlauts and typed both ways: the same person writes
 * `Stück` at a keyboard and `Stueck` on a phone in a hurry. Folding here means
 * the table below holds one spelling of each word instead of two, and `Esslöffel`
 * cannot be the one that was forgotten.
 */
export const foldUnit = (word: string): string =>
  word
    .toLowerCase()
    .replace(/\.$/, '')
    .replaceAll('ä', 'ae')
    .replaceAll('ö', 'oe')
    .replaceAll('ü', 'ue')
    .replaceAll('ß', 'ss');

/**
 * What people actually type, mapped to the wire codes.
 *
 * The server reads the same words the same way (`Domain/Recipes/UnitSpellings`)
 * for units that arrive without passing through here — another app's import, a
 * unit typed into the editor. Here they are read before anything is sent, so a
 * pasted line is shown back already understood.
 *
 * Both languages, because a German recipe says `EL` and an English one says
 * `tbsp`, and the same person writes both depending on where the recipe came
 * from. Plurals are listed rather than stripped: German plurals are not a
 * suffix rule, and a parser that guessed would read `Zitronen` as a unit.
 */
const spellings: Record<string, BuiltInUnit> = {
  g: 'g',
  gr: 'g',
  gramm: 'g',
  gram: 'g',
  grams: 'g',
  gramme: 'g',
  kg: 'kg',
  kilo: 'kg',
  kilos: 'kg',
  kilogramm: 'kg',
  kilogram: 'kg',
  kilograms: 'kg',
  ml: 'ml',
  milliliter: 'ml',
  millilitre: 'ml',
  milliliters: 'ml',
  millilitres: 'ml',
  l: 'l',
  liter: 'l',
  litre: 'l',
  liters: 'l',
  litres: 'l',
  tsp: 'tsp',
  tsps: 'tsp',
  teaspoon: 'tsp',
  teaspoons: 'tsp',
  tl: 'tsp',
  teeloeffel: 'tsp',
  tbsp: 'tbsp',
  tbsps: 'tbsp',
  tbs: 'tbsp',
  tablespoon: 'tbsp',
  tablespoons: 'tbsp',
  el: 'tbsp',
  essloeffel: 'tbsp',
  stk: 'piece',
  stueck: 'piece',
  piece: 'piece',
  pieces: 'piece',
  zehe: 'clove',
  zehen: 'clove',
  clove: 'clove',
  cloves: 'clove',
  bund: 'bunch',
  bunches: 'bunch',
  bunch: 'bunch',
  scheibe: 'slice',
  scheiben: 'slice',
  slice: 'slice',
  slices: 'slice',
  dose: 'can',
  dosen: 'can',
  can: 'can',
  cans: 'can',
  packung: 'pack',
  packungen: 'pack',
  paeckchen: 'pack',
  pack: 'pack',
  packs: 'pack',
  packet: 'pack',
  packets: 'pack',
  prise: 'pinch',
  prisen: 'pinch',
  pinch: 'pinch',
  pinches: 'pinch'
};

/** The built-in unit a written word spells — `Milliliter`, `EL`, `Stk.` — if any. */
export const spelledUnit = (word: string): BuiltInUnit | undefined => spellings[foldUnit(word)];
