<script lang="ts">
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import CookbookCover from './CookbookCover.svelte';
  import type { Cookbook } from './types';

  /**
   * One cookbook on a shelf.
   *
   * Built like a recipe card and for the same reason: a picture, a title in the
   * editorial face, and one line of fact underneath. No box — the cover is
   * already a rectangle, and putting it inside another one is two borders
   * saying the same thing.
   */
  interface Props {
    cookbook: Cookbook;
    /** Dimmed while a change to it is in flight. */
    pending?: boolean;
  }

  let { cookbook, pending = false }: Props = $props();
</script>

<article class="cookbook" class:pending aria-busy={pending || undefined}>
  <CookbookCover recipeIds={cookbook.coverRecipeIds} name={cookbook.name} />

  <h3 class="title">
    <a
      class="link"
      href={resolve('/(app)/cookbooks/[cookbookId]', { cookbookId: cookbook.id })}
      aria-label={m['cookbooks.card.open']({ name: cookbook.name })}
    >
      {cookbook.name}
    </a>
  </h3>

  <p class="meta">
    {m['cookbooks.card.count']({ count: cookbook.recipeCount })}
    <!-- Said in a word rather than drawn as a gear: this app has no icon
         vocabulary somebody would already know, and a symbol nobody can read is
         a decoration. -->
    {#if cookbook.kind === 'smart'}
      <span class="automatic">{m['cookbooks.kind.label']()}</span>
    {/if}
  </p>
</article>

<style>
  .cookbook {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  /* Applied while a write is in flight. Readable, visibly not settled. */
  .pending {
    opacity: 0.6;
    transition: opacity var(--duration-base) var(--ease-out);
  }

  .title {
    margin-top: var(--space-2);
    font-family: var(--font-editorial);
    font-size: var(--text-lg);
    line-height: 1.35;
    font-weight: var(--weight-regular);
    letter-spacing: -0.02em;
  }

  .link {
    display: block;
    scroll-margin-inline: var(--space-2);
    color: inherit;
    text-decoration: none;
  }

  /* The whole card is the link, so the target is the cookbook rather than the
     one or two words of its name. */
  .link::after {
    content: '';
    position: absolute;
    inset: 0;
  }

  .link:focus-visible::after {
    outline: 2px solid var(--border-focus);
    outline-offset: 4px;
    border-radius: var(--radius-lg);
  }

  .link:hover {
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }

  .meta {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .automatic {
    padding: 0 var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-size: var(--text-xs);
    letter-spacing: 0.06em;
    text-transform: uppercase;
  }
</style>
