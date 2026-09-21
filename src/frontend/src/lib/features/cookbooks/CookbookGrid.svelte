<script lang="ts">
  import { m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import CookbookCard from './CookbookCard.svelte';
  import CookbookCardSkeleton from './CookbookCardSkeleton.svelte';
  import type { Cookbook } from './types';

  /**
   * Every shelf, in columns.
   *
   * Tighter than the recipe grid — four across where recipes get three — because
   * a cover is a square and a name is two words, so more of them fit before the
   * page stops being scannable.
   */
  interface Props {
    cookbooks: readonly Cookbook[];
    loading?: boolean;
    pending?: readonly string[];
    /** Asked for the next page when the end of the list comes into view. */
    onmore?: () => void;
  }

  let { cookbooks, loading = false, pending = [], onmore }: Props = $props();

  const placeholders = [0, 1, 2, 3, 4, 5, 6, 7];

  const next = [0, 1, 2, 3];
</script>

{#if loading}
  <!-- A status rather than a bare div: a plain element is generic, and a
       generic element may not carry a name at all — the label was there for
       assistive technology and was being dropped on the floor by it. -->
  <div class="grid" role="status" aria-busy="true" aria-label={m['cookbooks.list.loading']()}>
    {#each placeholders as row (row)}
      <CookbookCardSkeleton />
    {/each}
  </div>
{:else}
  <ul class="grid">
    {#each cookbooks as cookbook (cookbook.id)}
      <li><CookbookCard {cookbook} pending={pending.includes(cookbook.id)} /></li>
    {/each}

    {#if onmore}
      {#each next as row (row)}
        <li aria-hidden="true" {@attach whenVisible(onmore)}><CookbookCardSkeleton /></li>
      {/each}
    {/if}
  </ul>
{/if}

<style>
  .grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-8);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    min-width: 0;
  }

  @media (min-width: 40rem) {
    .grid {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }
  }

  @media (min-width: 64rem) {
    .grid {
      grid-template-columns: repeat(4, minmax(0, 1fr));
    }
  }
</style>
