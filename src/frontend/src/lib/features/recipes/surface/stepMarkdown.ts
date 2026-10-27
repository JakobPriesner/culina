import type { StepSegment } from '../types';

import { atomsOf, isSpace, parseInline, type Atom, type Inline } from './stepInline';

export { parseInline };
export type { IngredientReference, Inline } from './stepInline';

/**
 * A step's text as Markdown, parsed over *segments* rather than a string so an ingredient reference stays one atom a delimiter cannot split.
 * A deliberately small subset (emphasis, code, links, lists); unknown syntax renders as typed. Output is data, never HTML.
 */

export type Block =
  | { readonly kind: 'paragraph'; readonly children: readonly Inline[] }
  | {
      readonly kind: 'list';
      readonly ordered: boolean;
      /** The author's first number; 1 for a bullet list. */
      readonly start: number;
      readonly items: readonly (readonly Inline[])[];
    };

export function parseStep(segments: readonly StepSegment[]): Block[] {
  return blocksOf(linesOf(atomsOf(segments)));
}

const linesOf = (atoms: readonly Atom[]): Atom[][] => {
  const lines: Atom[][] = [[]];

  for (const atom of atoms) {
    if (atom === '\n') {
      lines.push([]);
    } else {
      lines[lines.length - 1]!.push(atom);
    }
  }

  return lines;
};

/**
 * What a line is: bullet, number or prose; the space after the marker keeps `*emphasis*` from opening a list.
 * Bullets include the ones imported recipes carry (•, ·, ▪, en and em dash), a superset of what `parseRecipeText.ts` strips.
 */
function itemOf(line: readonly Atom[]): { start: number | null; content: Atom[] } | null {
  // A reference reads as a letter, so the space before it is not taken as part of the marker.
  const opening = line
    .slice(0, 8)
    .map((atom) => (typeof atom === 'string' ? atom : 'x'))
    .join('');
  const bullet = /^\s{0,3}[-*+•·▪–—]\s+/.exec(opening);
  const numbered = /^\s{0,3}(\d{1,3})[.)]\s+/.exec(opening);
  const marker = bullet ?? numbered;

  if (!marker) {
    return null;
  }

  return {
    start: numbered ? Number(numbered[1]) : null,
    content: line.slice(marker[0].length)
  };
}

const isBlank = (line: readonly Atom[]) => line.every((atom) => isSpace(atom));

/** An indented line under a list item continues it, as in Markdown. */
const isIndented = (line: readonly Atom[]) => isSpace(line[0]) && isSpace(line[1]);

const withoutIndent = (line: readonly Atom[]) =>
  line.slice(line.findIndex((atom) => !isSpace(atom)));

/** Groups lines into blocks; unlike CommonMark, line breaks inside a paragraph are kept because `StepText` renders them. */
function blocksOf(lines: readonly Atom[][]): Block[] {
  const blocks: Block[] = [];
  let paragraph: Atom[][] = [];
  let list: { ordered: boolean; start: number; items: Atom[][] } | null = null;

  const endParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push({ kind: 'paragraph', children: parseInline(joinLines(paragraph)) });
      paragraph = [];
    }
  };

  const endList = () => {
    if (list) {
      blocks.push({
        kind: 'list',
        ordered: list.ordered,
        start: list.start,
        items: list.items.map((item) => parseInline(item))
      });
      list = null;
    }
  };

  for (const line of lines) {
    const item = itemOf(line);

    if (item) {
      endParagraph();

      const ordered = item.start !== null;

      if (list && list.ordered !== ordered) {
        endList();
      }

      list ??= { ordered, start: item.start ?? 1, items: [] };
      list.items.push(item.content);
      continue;
    }

    if (list && !isBlank(line) && isIndented(line)) {
      list.items[list.items.length - 1]!.push('\n', ...withoutIndent(line));
      continue;
    }

    endList();

    if (isBlank(line)) {
      endParagraph();
    } else {
      paragraph.push([...line]);
    }
  }

  endParagraph();
  endList();

  return blocks;
}

const joinLines = (lines: readonly Atom[][]): Atom[] =>
  lines.flatMap((line, index) => (index === 0 ? line : ['\n', ...line]));
