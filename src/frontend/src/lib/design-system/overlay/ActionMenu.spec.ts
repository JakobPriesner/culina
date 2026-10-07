import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import ActionMenuHarness from '../__fixtures__/ActionMenuHarness.svelte';

/* jsdom has no popover, so `hidePopover` is stubbed onto the panel to see whether it was asked to close. */
function renderMenu() {
  const onchoose = vi.fn();
  const { container } = render(ActionMenuHarness, { onchoose });
  const panel = container.querySelector<HTMLElement>('[popover]')!;
  const hidden = vi.fn();

  panel.hidePopover = hidden;

  return { onchoose, hidden };
}

const inPanel = { hidden: true } as const;

describe('an action menu', () => {
  it('closes before a choice runs, and then runs it', async () => {
    const { onchoose, hidden } = renderMenu();
    const order: string[] = [];

    hidden.mockImplementation(() => order.push('closed'));
    onchoose.mockImplementation(() => order.push('chosen'));

    await fireEvent.click(screen.getByRole('button', { name: 'Rename', ...inPanel }));

    expect(order).toEqual(['closed', 'chosen']);
  });

  it('closes for a link too, without stopping it', async () => {
    const { hidden } = renderMenu();

    await fireEvent.click(screen.getByRole('link', { name: 'Elsewhere', ...inPanel }));

    expect(hidden).toHaveBeenCalledOnce();
  });

  it('stays open for a press that is not a choice', async () => {
    const { hidden } = renderMenu();

    await fireEvent.click(screen.getByText('Pick one'));

    expect(hidden).not.toHaveBeenCalled();
  });
});
