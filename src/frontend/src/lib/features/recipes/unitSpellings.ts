import type { BuiltInUnit } from './units';

/** Folds a typed word to the table's key form: umlauts are typed both ways (`Stück`, `Stueck`). */
export const foldUnit = (word: string): string =>
  word
    .toLowerCase()
    .replace(/\.$/, '')
    .replaceAll('ä', 'ae')
    .replaceAll('ö', 'oe')
    .replaceAll('ü', 'ue')
    .replaceAll('ß', 'ss');

/**
 * What people type, mapped to the wire codes; the server reads the same words
 * (`Domain/Recipes/UnitSpellings`).
 * Both languages, with plurals listed rather than stripped (German plurals aren't a suffix rule).
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

export const spelledUnit = (word: string): BuiltInUnit | undefined => spellings[foldUnit(word)];
