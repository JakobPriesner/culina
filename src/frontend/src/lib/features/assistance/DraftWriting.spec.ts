import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import DraftWriting from './DraftWriting.svelte';

import type { Draft } from './draftToRecipe';

const draft = (parts: Partial<Draft>): Draft => ({
  draftId: 'd1',
  groups: [],
  steps: [],
  tags: [],
  ...parts
});

describe('the draft being written', () => {
  it('says it is still writing, and stops saying so', () => {
    const { rerender } = render(DraftWriting, {
      draft: draft({ title: 'Auberginenauflauf' }),
      writing: true
    });

    expect(screen.getByText('Writing…')).toBeInTheDocument();

    rerender({ draft: draft({ title: 'Auberginenauflauf' }), writing: false });

    expect(screen.queryByText('Writing…')).not.toBeInTheDocument();
  });

  it('shows the parts that have arrived and nothing about the ones that have not', () => {
    render(DraftWriting, {
      draft: draft({
        title: 'Auberginenauflauf',
        groups: [
          {
            name: null,
            ingredients: [{ quantity: 2, unit: null, name: 'Auberginen', note: 'gewürfelt' }]
          }
        ]
      }),
      writing: true
    });

    expect(screen.getByRole('heading', { name: 'Auberginenauflauf' })).toBeInTheDocument();
    expect(screen.getByText(/2 Auberginen/)).toBeInTheDocument();

    // The steps have not been written yet, so there is no heading promising
    // them and no empty list standing where they will be.
    expect(screen.queryByText('Steps')).not.toBeInTheDocument();
  });

  it('is busy until it is not, so a reader is told once rather than per line', () => {
    const { container, rerender } = render(DraftWriting, { draft: null, writing: true });

    const region = container.querySelector('[aria-live="polite"]');

    expect(region).toHaveAttribute('aria-busy', 'true');

    rerender({ draft: draft({ title: 'Auberginenauflauf' }), writing: false });

    expect(region).toHaveAttribute('aria-busy', 'false');
  });
});
