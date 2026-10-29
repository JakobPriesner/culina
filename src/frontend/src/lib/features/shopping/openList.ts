import { resolve } from '$app/paths';
import { m } from '$shell/i18n';

/** The toast action after anything is put on the list from elsewhere, so the next step is one tap away. */
export const openListAction = {
  label: () => m['plan.openList'](),
  run: () => {
    // Imported on use: `$app/navigation` is not needed until somebody follows the toast.
    void import('$app/navigation').then(({ goto }) => goto(resolve('/(app)/week/shopping')));
  }
};

/** How many lists are on screen: its page, or the pane beside the week. */
let shown = 0;

/** Called by the list while it is on screen; returns the function that says it is gone. */
export function showingList(): () => void {
  shown += 1;

  return () => {
    shown -= 1;
  };
}

/** Whether the list is on screen, saying itself what arrives and what goes. */
export const listShown = () => shown > 0;

/** `openListAction`, unless the list is already on screen, where the lines arriving say it themselves. */
export const openList = () => (listShown() ? undefined : openListAction);
