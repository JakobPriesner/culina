<script lang="ts">
  import { m } from '$shell/i18n';
  import { whenVisible } from '$shell/whenVisible';

  import CookbookCard from './CookbookCard.svelte';
  import CookbookCardSkeleton from './CookbookCardSkeleton.svelte';
  import type { Cookbook } from './types';

  /**
   * Every shelf in columns, tighter than the recipe grid since covers are square and names short.
   */
  interface Props {
    cookbooks: readonly Cookbook[];
    loading?: boolean;
    pending?: readonly string[];
    onmore?: () => void;
  }

  let { cookbooks, loading = false, pending = [], onmore }: Props = $props();

  const placeholders = [0, 1, 2, 3, 4, 5, 6, 7];

  const next = [0, 1, 2, 3];
</script>

{#if loading}
  <!-- A status role, since a generic div may not carry the accessible name. -->
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
