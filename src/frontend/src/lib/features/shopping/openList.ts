import { resolve } from '$app/paths';
import { m } from '$shell/i18n';

/** The toast action after anything is put on the list from elsewhere, so the next step is one tap away. */
export const openListAction = {
  label: () => m['plan.openList'](),
  run: () => {
    // Imported on use: `$app/navigation` is not needed until somebody follows the toast.
    void import('$app/navigation').then(({ goto }) => goto(resolve('/(app)/shopping')));
  }
};
