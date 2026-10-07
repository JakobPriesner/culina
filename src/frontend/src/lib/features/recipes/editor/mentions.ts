import type { Ingredient, Step, StepSegment } from '../types';

/**
 * `@name` references from a step to an ingredient, kept as literal text so undo, input methods and screen readers just work.
 * Reading renders each as a pill with the scaled amount.
 */

const MARKER = '@';

/** Cap on how far back from the cursor a mention may start, so long steps are not rescanned per keystroke. */
const MAX_QUERY = 60;

/** Letters and digits in any script. */
const WORD = /[\p{L}\p{N}]/u;

const isWord = (character: string | undefined): boolean =>
  character !== undefined && WORD.test(character);

/** An `@` starts a mention only at a word start and before a word ("180 °C @ fan" and emails are not). */
const startsMention = (text: string, at: number): boolean =>
  !isWord(text[at - 1]) && text[at + 1] !== ' ' && text[at + 1] !== '\n';

export const toText = (step: Step): string =>
  step.segments
    .map((segment) => (segment.kind === 'text' ? segment.text : MARKER + segment.name))
    .join('');

/** Splits text into words and mentions, longest name first (`@olive oil` beats `@olive`); unknown names stay plain text. */
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

/** Whether the name is spelled out here as a whole word, so "butter" does not claim "@buttermilk". */
const matchesAt = (text: string, at: number, name: string): boolean =>
  text.slice(at, at + name.length).toLowerCase() === name.toLowerCase() &&
  !isWord(text[at + name.length]);

/** Links mentions written before the server gave a new ingredient its id; returns the same step when nothing changed. */
export function linkMentions(step: Step, ingredients: readonly Ingredient[]): Step {
  const segments = step.segments.flatMap((segment) =>
    segment.kind === 'text' ? toSegments(segment.text, ingredients) : [segment]
  );
  const linked = (list: readonly StepSegment[]) =>
    list.filter((segment) => segment.kind === 'ingredient').length;

  return linked(segments) === linked(step.segments) ? step : { ...step, segments };
}

export interface PendingMention {
  readonly at: number;
  readonly query: string;
}

/** The mention the cursor is inside, if any. The query may contain spaces; the picker closes itself once nothing matches. */
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

/** Whether the query is a finished name followed by more sentence ("butter melt." after "@butter "). */
export const writesOn = (query: string, ingredients: readonly Ingredient[]): boolean =>
  ingredients.some(
    (one) =>
      one.name &&
      query.toLowerCase().startsWith(one.name.toLowerCase()) &&
      /\s/.test(query[one.name.length] ?? '')
  );

export interface Insertion {
  readonly text: string;
  readonly caret: number;
}

/** Replaces the typed query with the chosen name, adding a trailing space unless one exists. */
export function insertMention(text: string, mention: PendingMention, name: string): Insertion {
  const after = mention.at + 1 + mention.query.length;
  const spaced = text[after] === ' ' ? '' : ' ';
  const written = MARKER + name + spaced;

  return {
    text: text.slice(0, mention.at) + written + text.slice(after),
    caret: mention.at + written.length
  };
}

/** Ingredients matching the query, prefix matches before substring matches. */
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
