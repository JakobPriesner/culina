import type { StepSegment } from '../types';

import { atomsOf, isSpace, parseInline, textOf, type Atom, type Inline } from './stepInline';

export { parseInline };
export type { IngredientReference, Inline } from './stepInline';

/**
 * A step's text, as Markdown.
 *
 * Steps arrive written by hand and imported from elsewhere, and both write
 * Markdown: a Tandoor instruction is Markdown by definition, and an author
 * typing `**bold**` means bold, not two asterisks. Printing the asterisks is
 * the bug this file exists to fix.
 *
 * It is a small subset on purpose — emphasis, code, links, and the two kinds of
 * list. A step is one instruction; headings, tables and block quotes have
 * nothing to say inside one, and every construct left out is one that renders
 * as the characters somebody typed, which is the right thing to do with syntax
 * this does not know.
 *
 * The parse runs over the step's *segments*, not over a string, because an
 * ingredient reference is not text and must survive intact — it is the thing
 * that makes "melt **180 g butter**" follow the portions. An atom is therefore
 * either one character or one whole reference, and a reference can no more be
 * split by a delimiter than a letter can.
 *
 * Nothing here produces HTML. The output is data, rendered by `StepInline` with
 * ordinary markup, so a recipe that arrives with a `<script>` in it is a recipe
 * with a `<script>` written on the page.
 */

export type Block =
  | { readonly kind: 'paragraph'; readonly children: readonly Inline[] }
  | {
      readonly kind: 'list';
      readonly ordered: boolean;
      readonly items: readonly (readonly Inline[])[];
    };

/** The step, as blocks to render. */
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
 * What a line is: a bullet, a number, or prose.
 *
 * The space after the marker is what keeps `*emphasis*` from opening a list —
 * a bullet is "`-` then a gap", and nothing else is.
 */
function itemOf(line: readonly Atom[]): { ordered: boolean; content: Atom[] } | null {
  const opening = textOf(line.slice(0, 8));
  const bullet = /^\s{0,3}[-*+]\s+/.exec(opening);
  const numbered = /^\s{0,3}\d{1,3}[.)]\s+/.exec(opening);
  const marker = bullet ?? numbered;

  if (!marker) {
    return null;
  }

  return { ordered: Boolean(numbered), content: line.slice(marker[0].length) };
}

const isBlank = (line: readonly Atom[]) => line.every((atom) => isSpace(atom));

/**
 * Lines grouped into blocks.
 *
 * A blank line ends what it follows, and a run of list lines is one list. The
 * line breaks *inside* a paragraph are kept rather than collapsed, which is
 * where this parts company with CommonMark on purpose: a step written as three
 * lines is three lines because somebody meant it to be, and `StepText` renders
 * it that way.
 */
function blocksOf(lines: readonly Atom[][]): Block[] {
  const blocks: Block[] = [];
  let paragraph: Atom[][] = [];
  let list: { ordered: boolean; items: Atom[][] } | null = null;

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
        items: list.items.map((item) => parseInline(item))
      });
      list = null;
    }
  };

  for (const line of lines) {
    const item = itemOf(line);

    if (item) {
      endParagraph();

      if (list && list.ordered !== item.ordered) {
        endList();
      }

      list ??= { ordered: item.ordered, items: [] };
      list.items.push(item.content);
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
