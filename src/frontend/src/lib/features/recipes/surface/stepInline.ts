import type { Quantity, StepSegment } from '../types';

/** Inline Markdown (emphasis, code, links) over atoms; the block half is `stepMarkdown.ts`. */

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

/** Longest first, so `**` is tried before `*` at the same position. */
const delimiters = [
  { marks: '**', kind: 'strong' },
  { marks: '__', kind: 'strong' },
  { marks: '~~', kind: 'strike' },
  { marks: '*', kind: 'emphasis' },
  { marks: '_', kind: 'emphasis' }
] as const;

/** Only http(s) and mailto links, never `javascript:`. */
const safeHref = (href: string) => /^(?:https?:\/\/|mailto:)/i.test(href);

export const isSpace = (atom: Atom | undefined) => typeof atom === 'string' && /\s/.test(atom);

/** An ingredient reference counts as a word character. */
const isWordCharacter = (atom: Atom | undefined) =>
  atom === undefined ? false : typeof atom !== 'string' || /[\p{L}\p{N}]/u.test(atom);

export const atomsOf = (segments: readonly StepSegment[]): Atom[] =>
  segments.flatMap((segment): Atom[] => (segment.kind === 'text' ? [...segment.text] : [segment]));

export const textOf = (atoms: readonly Atom[]) =>
  atoms.map((atom) => (typeof atom === 'string' ? atom : '')).join('');

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

    // A backslash makes the next character literal.
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

/** A code span at `start`; references inside stay literal, since the cook could no longer scale them. */
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
 * An emphasis, strong or strikethrough span at `start`. The opener needs a non-space after it and the closer a non-space before ("2 * 3 * 4" stays arithmetic);
 * `_` also may not touch word characters (`sous_vide_notes`).
 */
function spanAt(
  atoms: readonly Atom[],
  start: number
): { kind: 'strong' | 'emphasis' | 'strike'; inner: Atom[]; width: number } | null {
  for (const { marks, kind } of delimiters) {
    if (!runAt(atoms, start, marks)) {
      continue;
    }

    // `**` already failed to open; its second asterisk must not start an italic ("2 ** 3").
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
