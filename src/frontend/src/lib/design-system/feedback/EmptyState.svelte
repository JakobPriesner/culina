<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Nothing to show, and what to do about it.
   *
   * "No results" on its own is not an empty state: it tells someone what they
   * can already see. This says why the space is empty and offers the next step,
   * and it insists on distinguishing "you have not made one yet" from "your
   * filter matched nothing" — the second is a mistake to undo, the first is an
   * invitation.
   */
  interface Props {
    title: string;
    /** One or two sentences. Why it is empty, in the reader's terms. */
    body: string;
    /** A real button or link. An empty state without one is a dead end. */
    action: Snippet;
    /** An outline, not a picture: decoration here just delays reading. */
    icon?: Snippet;
  }

  let { title, body, action, icon }: Props = $props();
</script>

<div class="empty">
  {#if icon}
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
