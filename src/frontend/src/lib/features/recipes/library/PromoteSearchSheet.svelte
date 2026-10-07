<script lang="ts">
  import CookbookSheet from '$features/cookbooks/CookbookSheet.svelte';
  import { cookbooks } from '$features/cookbooks/stores/cookbooks.svelte';
  import type { CookbookRules } from '$features/cookbooks/types';
  import type { SavedSearch } from '$features/recipes/stores/savedSearches.svelte';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  interface Props {
    /** The saved search being offered as a shelf, which opens the sheet. */
    search: SavedSearch | null;
    householdId: string | null;
  }

  let { search = $bindable(), householdId }: Props = $props();

  let busy = $state(false);

  /**
   * A saved search, made into a shelf.
   *
   * The two are different things — a search is a lens, ordered and fuzzy; a
   * shelf is a curation that can be counted, drawn and taken to the shop — and
   * this is the one door between them. Only what a shelf can actually ask for
   * crosses: the tags and the time limit. The words stay behind, and the sheet
   * says so rather than quietly dropping them.
   */
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

<!-- A saved search, offered as a shelf. The same sheet the cookbooks page
     uses, so a cookbook made this way is made exactly like every other one. -->
<CookbookSheet
  open={search !== null}
  householdId={householdId ?? ''}
  {preset}
  saving={busy}
  onsave={(name, description, rules) => void make(name, description, rules)}
  onclose={() => (search = null)}
/>
