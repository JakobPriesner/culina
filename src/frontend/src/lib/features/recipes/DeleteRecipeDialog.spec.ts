import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import DeleteRecipeDialog from './DeleteRecipeDialog.svelte';
import { ErrorCodes, type AppError } from '$api';
import { renderWithProviders } from '$lib/test/render';

const render = (props: Record<string, unknown> = {}) => {
  const onconfirm = vi.fn();
  const onclose = vi.fn();

  renderWithProviders(DeleteRecipeDialog, {
    props: {
      open: true,
      title: 'Lemon orzo',
      deleting: false,
      error: null,
      onconfirm,
      onclose,
      ...props
    }
  });

  return { onconfirm, onclose };
};

/* jsdom has <dialog> but not the top layer, so showModal is the open state. */
beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('deleting a recipe', () => {
  it('names the recipe, and says how long it can still be brought back', () => {
    render();

    expect(screen.getByRole('dialog')).toHaveAccessibleName('Delete Lemon orzo?');
    expect(screen.getByText(/cooking history, notes and photos/)).toBeInTheDocument();
    expect(screen.getByText(/For 30 days you can still restore it/)).toBeInTheDocument();
  });

  it('keeps the recipe when the ordinary answer is pressed', async () => {
    const { onconfirm, onclose } = render();

    await userEvent.click(screen.getByRole('button', { name: 'Keep it' }));

    expect(onclose).toHaveBeenCalledOnce();
    expect(onconfirm).not.toHaveBeenCalled();
  });

  it('deletes only when asked to in so many words', async () => {
    const { onconfirm } = render();

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));

    expect(onconfirm).toHaveBeenCalledOnce();
  });

  it('says why it failed inside the dialog, where it can still be read', () => {
    const offline: AppError = {
      code: ErrorCodes.offline,
      detail: '',
      status: 0,
      requestId: null,
      fields: [],
      retryAfterSeconds: null
    };

    render({ error: offline });

    const alert = screen.getByRole('alert');

    expect(alert).toHaveTextContent("That recipe couldn't be deleted.");
    expect(alert).toHaveTextContent('You are offline.');
  });
});
