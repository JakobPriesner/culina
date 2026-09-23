import { screen } from '@testing-library/svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { clientError } from '$api';
import { renderWithProviders } from '$lib/test/render';
import DraftReview from './DraftReview.svelte';

import type { Recipe } from '$features/recipes/types';
import type { Draft } from './draftToRecipe';

const current: Recipe = {
  id: 'recipe-1',
  householdId: 'household-1',
  title: 'Tomato soup',
  description: null,
  language: 'en',
  yieldAmount: 4,
  yieldKind: 'servings',
  yieldLabel: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  imageId: null,
  groups: [],
  steps: [],
  tags: [],
  sourceUrl: null,
  createdBy: 'user-1',
  createdAt: '2026-09-20T12:00:00Z',
  updatedAt: '2026-09-20T12:00:00Z',
  version: 1
};

const partial: Draft = {
  draftId: 'draft-1',
  title: 'Roasted tomato soup',
  groups: [],
  steps: [],
  tags: []
};

/* jsdom has <dialog> but does not implement its modal top layer. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('the streamed recipe improvement review', () => {
  it('opens before the first draft event and then reveals what arrives', async () => {
    const props = {
      open: true,
      draft: null,
      current,
      writing: true,
      onaccept: vi.fn(),
      onclose: vi.fn()
    };
    const { rerender } = renderWithProviders(DraftReview, { props });

    expect(screen.getByRole('dialog', { name: 'AI suggestion' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Reading your recipe…');
    expect(
      screen.queryByRole('button', { name: 'Apply selected changes' })
    ).not.toBeInTheDocument();

    await rerender({ ...props, draft: partial });

    expect(screen.getByRole('status')).toHaveTextContent('The assistant is still writing');
    expect(screen.getByText('Roasted tomato soup')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Apply selected changes' })).toBeDisabled();
  });

  it('says why the assistant stopped, rather than that it suggested nothing', () => {
    renderWithProviders(DraftReview, {
      props: {
        open: true,
        draft: partial,
        current,
        writing: false,
        error: clientError('assistance.unavailable', 'The assistant could not be reached.'),
        onaccept: vi.fn(),
        onclose: vi.fn()
      }
    });

    // What was written before it stopped is still on offer beside the reason.
    expect(screen.getByText('Roasted tomato soup')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent("The AI couldn't be reached");
    expect(screen.queryByText('The AI did not suggest any changes.')).not.toBeInTheDocument();
  });
});
