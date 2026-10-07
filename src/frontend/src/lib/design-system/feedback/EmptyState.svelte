<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Nothing to show and what to do about it; says why the space is empty, telling "not made yet"
   * (an invitation) from "filter matched nothing" (a mistake to undo).
   */
  interface Props {
    title: string;
    body: string;
    action: Snippet;
    icon?: Snippet;
    /** A character in place of the icon, drawn without its frame. */
    art?: Snippet;
  }

  let { title, body, action, icon, art }: Props = $props();
</script>

<div class="empty">
  {#if art}
    <div class="art">{@render art()}</div>
  {:else if icon}
    <span class="icon" aria-hidden="true">{@render icon()}</span>
  {/if}

  <h2 class="title">{title}</h2>
  <p class="body">{body}</p>

  <div class="action">{@render action()}</div>
</div>

<style>
  .empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);
    max-width: 32rem;
    margin-inline: auto;
    padding: var(--space-16) var(--space-4);
    text-align: center;
  }

  .icon {
    display: block;
    width: var(--space-16);
    height: var(--space-16);
    margin-bottom: var(--space-3);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-accent-subtle);
    color: var(--accent);
  }

  .art {
    margin-bottom: var(--space-2);
  }

  .icon :global(svg) {
    display: block;
    width: 100%;
    height: 100%;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-2xl);
    font-weight: var(--weight-regular);
    letter-spacing: -0.025em;
  }

  .body {
    color: var(--text-muted);
    text-wrap: pretty;
  }

  .action {
    margin-top: var(--space-2);
  }
</style>
