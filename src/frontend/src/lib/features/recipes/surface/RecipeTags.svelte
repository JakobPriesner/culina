<script lang="ts">
  import { untrack } from 'svelte';

  import { resolve } from '$app/paths';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';

  /** The recipe's tags as pills under its head, each leading to the library filtered by it. */
  interface Props {
    recipeId: string;
    tags: readonly string[];
  }

  let { recipeId, tags }: Props = $props();

  /** A visitor on a shared link has no library to open, so there the tags are words. */
  const browsable = $derived(session.status === 'authenticated');

  /** Tags that arrived while the recipe was open (an answered diet question), lit up once so the answer is seen to land. */
  let fresh = $state<readonly string[]>([]);
  /** Plain, not $state: the last tags seen, compared inside the effect below. */
  let seen: { id: string; tags: readonly string[] } | null = null;

  $effect(() => {
    const id = recipeId;
    const now = tags;

    untrack(() => {
      const before = seen?.id === id ? seen.tags : null;

      fresh = before ? now.filter((tag) => !before.includes(tag)) : [];
      seen = { id, tags: now };
    });
  });

  const tagHref = (slug: string) => `${resolve('/(app)')}?tag=${encodeURIComponent(slug)}`;
</script>

{#if tags.length > 0}
  <ul class="tags" aria-label={m['editor.tags']()}>
    {#each tags as tag (tag)}
      <li>
        {#if browsable}
          <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the filter is appended. -->
          <a class="tag" class:fresh={fresh.includes(tag)} href={tagHref(tag)}>{tag}</a>
        {:else}
          <span class="tag">{tag}</span>
        {/if}
      </li>
    {/each}
  </ul>
{/if}

<style>
  .tags {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .tag {
    display: inline-flex;
    align-items: center;
    min-height: var(--control-sm);
    padding: 0 var(--space-3);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    font-size: var(--text-sm);
    text-decoration: none;
    overflow-wrap: normal;
  }

  @media (hover: hover) {
    a.tag:hover {
      border-color: var(--border-strong, var(--text-muted));
      color: var(--text);
    }
  }

  /* An answer seen to land: the accent once, then the pill settles into the row like the others. */
  .tag.fresh {
    animation: tag-arrived 2.4s var(--ease-out) both;
  }

  @keyframes tag-arrived {
    from {
      border-color: var(--accent);
      background: var(--surface-accent-subtle);
      color: var(--text);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .tag.fresh {
      animation: none;
      border-color: var(--accent);
    }
  }

  @media print {
    .tags {
      display: none;
    }
  }
</style>
