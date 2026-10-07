import type { AppError } from '$api';
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { restoreCookbook } from '$features/trash/trash';
import { explain } from '$shell/explain';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

import { addShelfToShoppingList } from './addShelfToShoppingList';
import { cookbooks } from './stores/cookbooks.svelte';
import type { CookbookDetail, CookbookRules } from './types';

/** What the actions need to know about the page they are on. */
interface Page {
  readonly cookbookId: () => string;
  readonly cookbook: () => CookbookDetail | null;
  readonly householdId: () => string | null;
  /** Reads the shelf and its header again, after something changed what is on it. */
  readonly reload: () => void;
}

/**
 * What can be done to the cookbook on screen, and the surfaces that are open
 * while it is being done.
 *
 * Every action here ends in a toast, a reload or a navigation — the page itself
 * is left with markup and with the one question it owns, which shelf is being
 * read.
 */
export function useCookbookActions(page: Page) {
  /** Which surface is open, and what each is waiting for. */
  const ui = $state({
    renaming: false,
    saving: false,
    picking: false,
    addingToList: false,
    confirmingDelete: false,
    deleting: false,
    deleteFailure: null as AppError | null
  });

  async function rename(name: string, description: string | null, rules: CookbookRules | null) {
    ui.saving = true;

    const done = await cookbooks.rename(page.cookbookId(), name, description, rules);

    ui.saving = false;

    if (done) {
      ui.renaming = false;
      // The rules decide what is on it, so changing them changes the shelf.
      page.reload();

      return;
    }

    const failure = cookbooks.error;

    toaster.show({
      message: () => (failure ? explain(failure) : m['cookbooks.add.failed']()),
      tone: 'danger'
    });
  }

  async function undoDelete(id: string) {
    const failure = await restoreCookbook(id);

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    await goto(resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: id }));
  }

  async function remove() {
    if (ui.deleting) {
      return;
    }

    const cookbookId = page.cookbookId();
    const name = page.cookbook()?.name ?? '';
    ui.deleting = true;
    const done = await cookbooks.remove(cookbookId);
    ui.deleting = false;

    if (!done) {
      ui.deleteFailure = cookbooks.error;

      return;
    }

    ui.confirmingDelete = false;
    toaster.show({
      message: () => m['cookbooks.delete.done']({ name }),
      action: { label: () => m['trash.undo'](), run: () => void undoDelete(cookbookId) }
    });

    await goto(resolve('/(app)/cookbooks'));
  }

  async function add(recipeId: string, title: string) {
    const cookbook = page.cookbook();

    if (!cookbook) {
      return;
    }

    const done = await cookbooks.setOn(recipeId, { id: cookbook.id, name: cookbook.name }, true);

    if (!done) {
      toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });

      return;
    }

    toaster.show({ message: () => m['cookbooks.addRecipes.added']({ title }) });
    page.reload();
  }

  /** The same tick, undone: a picker row that is on can be turned off again. */
  async function takeOff(recipeId: string, title: string) {
    const cookbook = page.cookbook();

    if (!cookbook) {
      return;
    }

    const done = await cookbooks.setOn(recipeId, { id: cookbook.id, name: cookbook.name }, false);

    const failure = cookbooks.error;

    toaster.show(
      done
        ? { message: () => m['cookbooks.addRecipes.removed']({ title }) }
        : {
            message: () => (failure ? explain(failure) : m['cookbooks.takeOff.failed']()),
            tone: 'danger'
          }
    );

    if (done) {
      page.reload();
    }
  }

  async function addToShoppingList() {
    const householdId = page.householdId();

    if (!householdId) {
      toaster.show({ message: () => m['cookbooks.shopping.empty']() });

      return;
    }

    ui.addingToList = true;

    const result = await addShelfToShoppingList(householdId, page.cookbookId());

    ui.addingToList = false;

    if (!('done' in result)) {
      toaster.show({ message: () => explain(result), tone: 'danger' });

      return;
    }

    const { done, total } = result;

    if (total === 0) {
      toaster.show({ message: () => m['cookbooks.shopping.empty']() });

      return;
    }

    toaster.show(
      done === total
        ? { message: () => m['cookbooks.shopping.done']({ count: done }) }
        : { message: () => m['cookbooks.shopping.partial']({ done, total }), tone: 'danger' }
    );
  }

  /** Opens the question of whether to delete, with no old failure left on it. */
  function askToDelete() {
    ui.deleteFailure = null;
    ui.confirmingDelete = true;
  }

  return { ui, askToDelete, rename, remove, add, takeOff, addToShoppingList };
}
