import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
import type { CookbookRules } from '$features/cookbooks/types';
import { m } from '$shell/i18n';
import { toaster } from '$shell/toaster.svelte';

/** The shelf being made from a search, while its sheet is open. */
export function createShelving(householdId: () => string) {
  let current = $state<{ name: string; rules: CookbookRules } | null>(null);
  let busy = $state(false);

  return {
    get current() {
      return current;
    },
    get busy() {
      return busy;
    },

    offer(name: string, rules: CookbookRules) {
      current = { name, rules };
    },

    close() {
      current = null;
    },

    async make(name: string, description: string | null, rules: CookbookRules | null) {
      if (!rules) {
        return;
      }

      busy = true;

      const made = await cookbooks.create(householdId(), name, description ?? undefined, rules);

      busy = false;

      if (made) {
        current = null;
        toaster.show({ message: () => m['saved.promoted']({ name }) });

        return;
      }

      toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });
    }
  };
}
