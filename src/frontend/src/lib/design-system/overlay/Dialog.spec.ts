import { screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import DialogHarness from '../__fixtures__/DialogHarness.svelte';
import { renderWithProviders } from '$lib/test/render';

/*
 * The browser does the focus trap and the inert background, which is precisely
 * why these assertions exist: they prove the native dialog is actually being
 * opened as modal, rather than rendered as an ordinary box that looks right.
 */
const open = () => screen.getByRole('button', { name: 'Open' });
const dialog = () => screen.getByRole('dialog');

/*
 * jsdom implements <dialog> but not the top layer, so showModal/close are
 * stubbed to the observable parts: the open state and the close event.
 */
beforeEach(() => {
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

    // What Escape does natively: the browser closes the element and fires
    // `close`, and the component has to notice rather than being told.
    (screen.getByRole('dialog') as HTMLDialogElement).close();

    await waitFor(() => expect(onclose).toHaveBeenCalledOnce());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  /*
   * Every way out reports, not only the native one. A caller that passes
   * `open` as an expression rather than a binding — `open={chosen !== null}` —
   * hears about a dismissal through this and nothing else, so a Close button
   * that stayed quiet would leave it believing the dialog was still up.
   */
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
