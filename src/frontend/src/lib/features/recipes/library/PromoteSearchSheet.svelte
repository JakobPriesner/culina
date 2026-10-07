<script lang="ts">
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import type { CookbookRules } from '$features/cookbooks/types';
  import type { SavedSearch } from '$features/recipes/stores/savedSearches.svelte';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  interface Props {
    /** The saved search offered as a shelf; non-null opens the sheet. */
    search: SavedSearch | null;
    householdId: string | null;
  }

  let { search = $bindable(), householdId }: Props = $props();

  let busy = $state(false);

  /** Only tags and the time limit carry over to a shelf; the typed words stay behind (the sheet says so). */
  const preset = $derived(
    search
      ? {
          name: search.name,
          rules: { tags: [...search.tags], ingredients: [], maxMinutes: search.maxMinutes }
        }
      : null
  );

  async function make(name: string, description: string | null, rules: CookbookRules | null) {
    if (!householdId || !rules) {
      return;
    }

    busy = true;

    const made = await cookbooks.create(householdId, name, description ?? undefined, rules);

    busy = false;

    if (made) {
      search = null;
      toaster.show({ message: () => m['saved.promoted']({ name }) });

      return;
    }

    toaster.show({ message: () => m['cookbooks.add.failed'](), tone: 'danger' });
  }
</script>

<CookbookSheet
  open={search !== null}
  householdId={householdId ?? ''}
  {preset}
  saving={busy}
  onsave={(name, description, rules) => void make(name, description, rules)}
  onclose={() => (search = null)}
/>
