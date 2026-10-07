import type { Quantity, StepSegment } from '../types';

/**
 * The inline half of a step's Markdown: emphasis, code and links over atoms,
 * where an atom is one character or one whole ingredient reference. The
 * block half — paragraphs and lists — is `stepMarkdown.ts`.
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

/** One character, or one ingredient reference. */
export type Atom = string | IngredientReference;

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

export const isSpace = (atom: Atom | undefined) => typeof atom === 'string' && /\s/.test(atom);

/** Nothing at all is not a word character; an ingredient reference is. */
const isWordCharacter = (atom: Atom | undefined) =>
  atom === undefined ? false : typeof atom !== 'string' || /[\p{L}\p{N}]/u.test(atom);

export const atomsOf = (segments: readonly StepSegment[]): Atom[] =>
  segments.flatMap((segment): Atom[] => (segment.kind === 'text' ? [...segment.text] : [segment]));

export const textOf = (atoms: readonly Atom[]) =>
  atoms.map((atom) => (typeof atom === 'string' ? atom : '')).join('');

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
