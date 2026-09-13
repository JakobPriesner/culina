import type { Ingredient, Step, StepSegment } from '../types';

/**
 * How an author points a step at one of the recipe's own ingredients.
 *
 * A step that references an ingredient can print that ingredient's *scaled*
 * amount — "Melt **180 g butter**" rather than "melt the butter" with the
 * number half a screen away. The reference is the whole reason steps are stored
 * as segments rather than as a sentence.
 *
 * The author writes the reference as `@butter`, the same gesture they already
 * use everywhere else to point at something inside text. It is deliberately
 * literal: what they type is what is stored, so undo is the browser's, every
 * keyboard and input method works, a screen reader reads a sentence, and a
 * mention can be removed by pressing backspace like any other word. The pill
 * with the amount in it is what *reading* renders — that is where it helps.
 *
 * The previous behaviour was to scan each step for any ingredient name it
 * contained and link whatever turned up. It cost the author nothing, which was
 * the appeal, but it was invisible, it linked "oil" inside "olive oil", and
 * there was no way to say "not that one". A reference you cannot see is a
 * reference you cannot correct.
 */

/** What starts a mention. */
const MARKER = '@';

/**
 * How far back a mention may begin from the cursor.
 *
 * An ingredient name is short, and without a limit every keystroke in a long
 * step would rescan the paragraph looking for an `@` that is not there.
 */
const MAX_QUERY = 60;

/** Letters and digits, in any script — German and English both need more than a-z. */
const WORD = /[\p{L}\p{N}]/u;

const isWord = (character: string | undefined): boolean =>
  character !== undefined && WORD.test(character);

/**
 * Whether an `@` at this position starts a mention rather than being an `@`.
 *
 * "180 °C @ fan" is not a mention, and neither is an email address. A mention
 * begins a word and is immediately followed by one.
 */
const startsMention = (text: string, at: number): boolean =>
  !isWord(text[at - 1]) && text[at + 1] !== ' ' && text[at + 1] !== '\n';

/** The sentence an author sees and edits, mentions included. */
export const toText = (step: Step): string =>
  step.segments
    .map((segment) => (segment.kind === 'text' ? segment.text : MARKER + segment.name))
    .join('');

/**
 * Splits a sentence into words and the mentions it contains.
 *
 * Longest name first, so `@olive oil` is one mention rather than `@olive`
 * followed by the word "oil". A mention that names nothing in the list — an
 * ingredient since removed, or a name typed by hand — stays as plain text,
 * which is the honest reading of it.
 */
export function toSegments(text: string, ingredients: readonly Ingredient[]): StepSegment[] {
  // An ingredient the server has not seen has no id to reference yet.
  const named = ingredients
    .filter((one) => one.id && one.name)
    .sort((a, b) => b.name.length - a.name.length);

  const segments: StepSegment[] = [];
  let plain = '';
  let index = 0;

  const flush = () => {
    if (plain) {
      segments.push({ kind: 'text', text: plain });
      plain = '';
    }
  };

  while (index < text.length) {
    const found =
      text[index] === MARKER && startsMention(text, index)
        ? named.find((one) => matchesAt(text, index + 1, one.name))
        : undefined;

    if (!found) {
      plain += text[index];
      index += 1;
      continue;
    }

    flush();
    segments.push({
      kind: 'ingredient',
      ingredientId: found.id,
      name: found.name,
      quantity: found.quantity
    });
    index += 1 + found.name.length;
  }

  flush();

  return segments;
}

/**
 * Whether this name is spelled out at this position, as a whole word.
 *
 * The trailing check is what stops the ingredient "butter" from claiming the
 * first half of "@buttermilk".
 */
const matchesAt = (text: string, at: number, name: string): boolean =>
  text.slice(at, at + name.length).toLowerCase() === name.toLowerCase() &&
  !isWord(text[at + name.length]);

/** A mention the author is in the middle of typing. */
export interface PendingMention {
  /** Where its `@` is. */
  readonly at: number;
  /** What has been typed after the `@`, which may be empty. */
  readonly query: string;
}

/**
 * The mention the cursor is inside, if it is inside one.
 *
 * The query may contain spaces, because ingredient names do — "olive oil" is
 * one name. Nothing here decides when to stop offering suggestions; the picker
 * closes itself once a query matches nothing, which handles "@olive oil in the
 * pan" without a rule about where a name ends.
 */
export function pendingMention(text: string, caret: number): PendingMention | null {
  const earliest = Math.max(0, caret - MAX_QUERY - 1);

  for (let at = caret - 1; at >= earliest; at -= 1) {
    const character = text[at];

    // A mention lives on one line, and a second `@` starts a new one.
    if (character === '\n' || (character === MARKER && !startsMention(text, at))) {
      return null;
    }

    if (character === MARKER) {
      return { at, query: text.slice(at + 1, caret) };
    }
  }

  return null;
}

/** A sentence with a mention written into it, and where to put the cursor. */
export interface Insertion {
  readonly text: string;
  readonly caret: number;
}

/**
 * Replaces what was typed after the `@` with the name that was chosen.
 *
 * A space follows the mention unless one already does, so the next word does
 * not have to be separated by hand.
 */
export function insertMention(text: string, mention: PendingMention, name: string): Insertion {
  const after = mention.at + 1 + mention.query.length;
  const spaced = text[after] === ' ' ? '' : ' ';
  const written = MARKER + name + spaced;

  return {
    text: text.slice(0, mention.at) + written + text.slice(after),
    caret: mention.at + written.length
  };
}

/**
 * The ingredients a query offers, best first.
 *
 * A name that starts with what was typed comes before one that merely contains
 * it: someone typing "oil" means the oil, not the "boiled potatoes".
 */
export function suggest(query: string, ingredients: readonly Ingredient[]): readonly Ingredient[] {
  const wanted = query.trim().toLowerCase();

  if (!wanted) {
    return ingredients.filter((one) => one.id);
  }

  const matches = ingredients.filter((one) => one.id && one.name.toLowerCase().includes(wanted));

  return matches.sort((a, b) => {
    const byStart =
      Number(b.name.toLowerCase().startsWith(wanted)) -
      Number(a.name.toLowerCase().startsWith(wanted));

    return byStart || a.name.length - b.name.length;
  });
}
