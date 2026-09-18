import type { Quantity, StepSegment } from '../types';

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

export interface IngredientReference {
  readonly kind: 'ingredient';
  readonly ingredientId: string;
  readonly name: string;
  readonly quantity: Quantity;
}

export type Inline =
  | { readonly kind: 'text'; readonly text: string }
  | IngredientReference
  | { readonly kind: 'code'; readonly text: string }
  | { readonly kind: 'link'; readonly href: string; readonly children: readonly Inline[] }
  | {
      readonly kind: 'strong' | 'emphasis' | 'strike';
      readonly children: readonly Inline[];
    };

export type Block =
  | { readonly kind: 'paragraph'; readonly children: readonly Inline[] }
  | {
      readonly kind: 'list';
      readonly ordered: boolean;
      readonly items: readonly (readonly Inline[])[];
    };

/** One character, or one ingredient reference. */
type Atom = string | IngredientReference;

/**
 * The delimiters, longest first.
 *
 * Order is the whole of why `**bold**` is not an empty italic: `**` is tried
 * before `*` at the same position.
 */
const delimiters = [
  { marks: '**', kind: 'strong' },
  { marks: '__', kind: 'strong' },
  { marks: '~~', kind: 'strike' },
  { marks: '*', kind: 'emphasis' },
  { marks: '_', kind: 'emphasis' }
] as const;

/** A link has to go somewhere a link can go. `javascript:` is not a place. */
const safeHref = (href: string) => /^(?:https?:\/\/|mailto:)/i.test(href);

const isSpace = (atom: Atom | undefined) => typeof atom === 'string' && /\s/.test(atom);

/** Nothing at all is not a word character; an ingredient reference is. */
const isWordCharacter = (atom: Atom | undefined) =>
  atom === undefined ? false : typeof atom !== 'string' || /[\p{L}\p{N}]/u.test(atom);

const atomsOf = (segments: readonly StepSegment[]): Atom[] =>
  segments.flatMap((segment): Atom[] => (segment.kind === 'text' ? [...segment.text] : [segment]));

const textOf = (atoms: readonly Atom[]) =>
  atoms.map((atom) => (typeof atom === 'string' ? atom : '')).join('');

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

/** The atoms of one paragraph or list item, as formatted pieces. */
export function parseInline(atoms: readonly Atom[]): Inline[] {
  const nodes: Inline[] = [];
  let pending = '';
  let index = 0;

  const flush = () => {
    if (pending !== '') {
      nodes.push({ kind: 'text', text: pending });
      pending = '';
    }
  };

  const take = (node: Inline, width: number) => {
    flush();
    nodes.push(node);
    index += width;
  };

  while (index < atoms.length) {
    const atom = atoms[index]!;

    if (typeof atom !== 'string') {
      take(atom, 1);
      continue;
    }

    // A backslash spends itself on the next character, which is how somebody
    // writes an asterisk they mean literally.
    if (atom === '\\' && typeof atoms[index + 1] === 'string') {
      pending += atoms[index + 1] as string;
      index += 2;
      continue;
    }

    const code = codeAt(atoms, index);

    if (code) {
      take({ kind: 'code', text: code.text }, code.width);
      continue;
    }

    const link = linkAt(atoms, index);

    if (link) {
      take({ kind: 'link', href: link.href, children: parseInline(link.label) }, link.width);
      continue;
    }

    const span = spanAt(atoms, index);

    if (span) {
      take({ kind: span.kind, children: parseInline(span.inner) }, span.width);
      continue;
    }

    pending += atom;
    index += 1;
  }

  flush();

  return nodes;
}

/**
 * A code span at `start`, if one opens there.
 *
 * Its contents are characters and nothing else: a reference inside backticks is
 * a reference the cook can no longer scale, so the backticks stay literal
 * rather than swallowing it.
 */
function codeAt(atoms: readonly Atom[], start: number): { text: string; width: number } | null {
  if (atoms[start] !== '`') {
    return null;
  }

  for (let end = start + 1; end < atoms.length; end += 1) {
    if (typeof atoms[end] !== 'string') {
      return null;
    }

    if (atoms[end] === '`' && end > start + 1) {
      return { text: textOf(atoms.slice(start + 1, end)), width: end - start + 1 };
    }
  }

  return null;
}

/** A `[label](href)` at `start`, if one opens there and goes somewhere real. */
function linkAt(
  atoms: readonly Atom[],
  start: number
): { label: Atom[]; href: string; width: number } | null {
  if (atoms[start] !== '[') {
    return null;
  }

  const close = atoms.indexOf(']', start + 1);

  if (close < 0 || atoms[close + 1] !== '(') {
    return null;
  }

  const end = atoms.indexOf(')', close + 2);

  if (end < 0) {
    return null;
  }

  const href = textOf(atoms.slice(close + 2, end)).trim();

  if (!safeHref(href)) {
    return null;
  }

  return { label: atoms.slice(start + 1, close), href, width: end - start + 1 };
}

/**
 * An emphasis, strong or strikethrough span at `start`, if one opens there.
 *
 * The two rules that stop a sentence from being mangled: a run that opens is
 * followed by something other than a space, and the run that closes it is
 * preceded by something other than a space — so "2 * 3 * 4" is arithmetic. For
 * `_` there is a third, that neither end sits inside a word, which is what
 * leaves `sous_vide_notes` alone.
 */
function spanAt(
  atoms: readonly Atom[],
  start: number
): { kind: 'strong' | 'emphasis' | 'strike'; inner: Atom[]; width: number } | null {
  for (const { marks, kind } of delimiters) {
    if (!runAt(atoms, start, marks)) {
      continue;
    }

    // `**` was tried first and did not open. The second asterisk of a run is
    // not the start of an italic, so "2 ** 3" stays arithmetic.
    if (marks.length === 1 && atoms[start + 1] === marks) {
      continue;
    }

    const from = start + marks.length;

    if (isSpace(atoms[from]) || atoms[from] === undefined) {
      continue;
    }

    if (marks.startsWith('_') && isWordCharacter(atoms[start - 1])) {
      continue;
    }

    for (let end = from + 1; end + marks.length <= atoms.length; end += 1) {
      if (!runAt(atoms, end, marks) || isSpace(atoms[end - 1])) {
        continue;
      }

      if (marks.startsWith('_') && isWordCharacter(atoms[end + marks.length])) {
        continue;
      }

      return { kind, inner: atoms.slice(from, end), width: end + marks.length - start };
    }
  }

  return null;
}

const runAt = (atoms: readonly Atom[], at: number, marks: string) =>
  [...marks].every((mark, offset) => atoms[at + offset] === mark);
