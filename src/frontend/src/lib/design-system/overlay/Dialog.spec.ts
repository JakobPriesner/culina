import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import DialogHarness from '../__fixtures__/DialogHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

// These prove the native dialog is opened as modal, since the browser provides the focus trap and inert background.
const open = () => screen.getByRole('button', { name: 'Open' });
const dialog = () => screen.getByRole('dialog');

// jsdom has no top layer, so showModal/close are stubbed to the open state and close event.
beforeEach(() => {
  // jsdom has no animations; a test that wants an exit to wait for gives it one.
  Reflect.deleteProperty(HTMLDialogElement.prototype, 'getAnimations');
  HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
    this.open = true;
  };

  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

describe('a modal dialog', () => {
  it('is not there until it is opened', () => {
    renderWithProviders(DialogHarness, {});

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('is named by its title, so it is not announced as an unlabelled box', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());

    expect(dialog()).toHaveAccessibleName('Rename recipe');
  });

  it('locks the page behind it', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());

    expect(document.body.style.overflow).toBe('hidden');
  });

  it('gives the page back when it closes', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    await waitFor(() => expect(document.body.style.overflow).not.toBe('hidden'));
  });

  it('offers a way out that can be seen, not only Escape and the backdrop', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());

    expect(screen.getByRole('button', { name: 'Close' })).toBeVisible();
  });

  it('tells the caller when the browser closes it with Escape', async () => {
    const onclose = vi.fn();

    renderWithProviders(DialogHarness, { props: { onclose } });

    await userEvent.click(open());

    // Native Escape: the browser closes the element and fires `close`.
    (screen.getByRole('dialog') as HTMLDialogElement).close();

    await waitFor(() => expect(onclose).toHaveBeenCalledOnce());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  // Every way out must report: a caller passing `open` as an expression hears of dismissal only via onclose.
  it('tells the caller when it is closed from the visible button', async () => {
    const onclose = vi.fn();

    renderWithProviders(DialogHarness, { props: { onclose } });

    await userEvent.click(open());
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    await waitFor(() => expect(onclose).toHaveBeenCalledOnce());
  });

  it('closes from inside, so an action can finish the task', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

// Pages mount every sheet they might offer, so content must not be built until opened.
describe("a dialog's content", () => {
  const field = () => screen.queryByRole('textbox', { hidden: true });

  it('is not built until the dialog is opened', async () => {
    renderWithProviders(DialogHarness, {});

    expect(field()).not.toBeInTheDocument();

    await userEvent.click(open());

    expect(field()).toBeInTheDocument();
  });

  it('is dropped once the dialog has closed, so reopening starts clean', async () => {
    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());
    await userEvent.type(field()!, 'Soup');
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    await waitFor(() => expect(field()).not.toBeInTheDocument());
  });

  it('stays while the exit is still being painted', async () => {
    let finishExit!: () => void;
    const exit = new Promise<void>((resolve) => (finishExit = resolve));

    HTMLDialogElement.prototype.getAnimations = () => [{ finished: exit } as unknown as Animation];

    renderWithProviders(DialogHarness, {});

    await userEvent.click(open());
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(field()).toBeInTheDocument();

    finishExit();

    await waitFor(() => expect(field()).not.toBeInTheDocument());
  });
});
