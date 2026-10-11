import { foldedWords } from '../foldText';

/**
 * Search is the app's, not only the library's: a typed word can also mean a page ("Einkauf"), an action
 * ("Rezept einfügen"), a cookbook or something on the shopping list. Each is a place to go.
 */
export interface Place {
  readonly key: string;
  readonly label: string;
  /** What kind of place it is, beside the label: "Kochbuch", "Auf der Einkaufsliste". */
  readonly detail?: string;
  readonly href: string;
  /** Other words it answers to ("Liste", "Woche"), folded like the label. */
  readonly keywords?: readonly string[];
}

/** At most this many: places come first and must not push the recipes out of sight. */
const most = 5;

/** Every typed word starts a word of the place's label or keywords; one letter matches nothing. */
export function matchPlaces(typed: string, places: readonly Place[]): Place[] {
  const wanted = foldedWords(typed);

  if (wanted.length === 0 || typed.trim().length < 2) {
    return [];
  }

  return places
    .filter((place) => {
      const own = [place.label, ...(place.keywords ?? [])].flatMap((text) => foldedWords(text));

      return wanted.every((word) => own.some((candidate) => candidate.startsWith(word)));
    })
    .slice(0, most);
}
