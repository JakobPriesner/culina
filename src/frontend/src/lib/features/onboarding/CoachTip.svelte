<script lang="ts">
  import type { Snippet } from 'svelte';

  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import { tips, type TipId } from './tips.svelte';

  /**
   * A tip beside the thing it explains, the first time it is used, and never again after "Got it". Quiet on
   * purpose: a tinted card in the flow, never over a control.
   */
  interface Props {
    id: TipId;
    title: string;
    children: Snippet;
    /** Something to do about it, besides putting it away. */
    action?: Snippet;
  }

  let { id, title, children, action }: Props = $props();
</script>

{#if tips.isNew(id)}
  <aside class="tip" aria-label={title}>
    <p class="title">{title}</p>
    <div class="body">{@render children()}</div>
    <div class="actions">
      {@render action?.()}
      <Button size="sm" variant="ghost" onclick={() => tips.dismiss(id)}>{m['tips.gotIt']()}</Button
      >
    </div>
  </aside>
{/if}

<style>
  .tip {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-bottom: var(--space-4);
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
    font-size: var(--text-sm);
  }

  .title {
    margin: 0;
    font-weight: var(--weight-semibold);
  }

  .body {
    color: var(--text);
    line-height: var(--leading-normal);
  }

  .body :global(ul) {
    margin: 0;
    padding-inline-start: var(--space-4);
  }

  .body :global(li + li) {
    margin-top: var(--space-1);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  @media print {
    .tip {
      display: none;
    }
  }
</style>
