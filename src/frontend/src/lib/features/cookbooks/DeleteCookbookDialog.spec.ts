import { screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ErrorCodes, type AppError } from '$api';
import { renderWithProviders } from '$lib/test/render';

import DeleteCookbookDialog from './DeleteCookbookDialog.svelte';

const render = (props: Record<string, unknown> = {}) => {
  const onconfirm = vi.fn();
  const onclose = vi.fn();

  renderWithProviders(DeleteCookbookDialog, {
    props: {
      open: true,
      name: 'Weeknights',
      deleting: false,
      error: null,
      onconfirm,
      onclose,
      ...props
    }
  });

  return { onconfirm, onclose };
};

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('deleting a cookbook', () => {
  it('names the cookbook and says that its recipes remain', () => {
    render();

    expect(screen.getByRole('dialog')).toHaveAccessibleName('Delete “Weeknights”?');
    expect(screen.getByText(/recipes stay in your library/i)).toBeInTheDocument();
  });

  it('keeps the cookbook when the safe answer is pressed', async () => {
    const { onconfirm, onclose } = render();

    await userEvent.click(screen.getByRole('button', { name: 'Keep cookbook' }));

    expect(onclose).toHaveBeenCalledOnce();
    expect(onconfirm).not.toHaveBeenCalled();
  });

  it('deletes only after an explicit confirmation', async () => {
    const { onconfirm } = render();

    await userEvent.click(screen.getByRole('button', { name: 'Delete this cookbook' }));

    expect(onconfirm).toHaveBeenCalledOnce();
  });

  it('keeps a failure inside the dialog', () => {
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

    expect(alert).toHaveTextContent('That cookbook could not be deleted.');
    expect(alert).toHaveTextContent('You are offline.');
  });
});
