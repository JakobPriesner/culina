import { describe, expect, it } from 'vitest';

import { parseStep, type Block, type Inline } from './stepMarkdown';
import type { StepSegment } from '../types';

const text = (value: string): StepSegment => ({ kind: 'text', text: value });

const butter: StepSegment = {
  kind: 'ingredient',
  ingredientId: 'i-butter',
  name: 'butter',
  quantity: { value: 200, unit: 'g' }
};

/** What a block reads as, with the formatting written back in as brackets. */
const shape = (nodes: readonly Inline[]): string =>
  nodes
    .map((node) => {
      switch (node.kind) {
        case 'text':
          return node.text;
        case 'ingredient':
          return `@${node.name}`;
        case 'code':
          return `\`${node.text}\``;
        case 'link':
          return `link(${node.href}){${shape(node.children)}}`;
        default:
          return `${node.kind}{${shape(node.children)}}`;
      }
    })
    .join('');

const only = (markdown: string): Block => parseStep([text(markdown)])[0]!;

const inline = (markdown: string): string => {
  const block = only(markdown);

  expect(block.kind).toBe('paragraph');

  return shape(block.kind === 'paragraph' ? block.children : []);
};

describe('parseStep', () => {
  it.each([
    ['**salt** it', 'strong{salt} it'],
    ['__salt__ it', 'strong{salt} it'],
    ['*gently*', 'emphasis{gently}'],
    ['_gently_', 'emphasis{gently}'],
    ['~~skip~~ this', 'strike{skip} this'],
    ['**do *not* stir**', 'strong{do emphasis{not} stir}'],
    ['a `250 °C` oven', 'a `250 °C` oven']
  ])('renders %s', (markdown, expected) => {
    expect(inline(markdown)).toBe(expected);
  });

  it.each([
    // Arithmetic, not emphasis: a delimiter next to a space opens nothing.
    ['2 * 3 * 4', '2 * 3 * 4'],
    ['2 ** 3 ** 4', '2 ** 3 ** 4'],
    // Inside a word, an underscore is part of the word.
    ['sous_vide_notes', 'sous_vide_notes'],
    // Nothing closes these, so they are the characters somebody typed.
    ['a * lonely star', 'a * lonely star'],
    ['3 ~ 4 minutes', '3 ~ 4 minutes'],
    // Syntax this does not know renders as itself rather than disappearing.
    ['## not a heading', '## not a heading'],
    ['| a | b |', '| a | b |']
  ])('leaves %s alone', (markdown, expected) => {
    expect(inline(markdown)).toBe(expected);
  });

  it('lets a backslash buy a literal asterisk', () => {
    expect(inline('2 \\* 3 is \\*not\\* six')).toBe('2 * 3 is *not* six');
  });

  it('links only where a link can go', () => {
    expect(inline('see [the source](https://example.com/x)')).toBe(
      'see link(https://example.com/x){the source}'
    );
    expect(inline('[click](javascript:alert(1))')).toBe('[click](javascript:alert(1))');
  });

  it('never splits an ingredient reference', () => {
    const block = only('');
    expect(block).toBeUndefined();

    const [paragraph] = parseStep([text('Melt **'), butter, text('** slowly*')]);

    // The reference survives the emphasis that surrounds it, amounts and all.
    expect(paragraph?.kind === 'paragraph' && shape(paragraph.children)).toBe(
      'Melt strong{@butter} slowly*'
    );
  });

  it('keeps the line breaks inside a paragraph and splits on a blank one', () => {
    const blocks = parseStep([text('Bake.\nRest.\n\nSlice.')]);

    expect(blocks).toHaveLength(2);
    expect(blocks[0]?.kind === 'paragraph' && shape(blocks[0].children)).toBe('Bake.\nRest.');
    expect(blocks[1]?.kind === 'paragraph' && shape(blocks[1].children)).toBe('Slice.');
  });

  it.each([
    ['- salt\n- pepper', false],
    ['* salt\n* pepper', false],
    ['1. salt\n2. pepper', true]
  ])('reads %s as a list', (markdown, ordered) => {
    const [block] = parseStep([text(markdown)]);

    expect(block?.kind === 'list' && block.ordered).toBe(ordered);
    expect(block?.kind === 'list' && block.items.map(shape)).toEqual(['salt', 'pepper']);
  });

  it('starts a new list when the marker changes kind', () => {
    const blocks = parseStep([text('Prep:\n- salt\n1. pepper')]);

    expect(blocks.map((block) => block.kind)).toEqual(['paragraph', 'list', 'list']);
  });
});
