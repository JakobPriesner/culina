<script lang="ts">
  import type { Snippet } from 'svelte';

  interface Props {
    title: string;
    subtitle?: string;
    eyebrow?: string;
    actions?: Snippet;
  }

  let { title, subtitle, eyebrow, actions }: Props = $props();
</script>

<header class="page-header">
  <div class="heading">
    {#if eyebrow}<p class="eyebrow">{eyebrow}</p>{/if}
    <h1>{title}</h1>
    {#if subtitle}<p class="subtitle">{subtitle}</p>{/if}
  </div>
  {#if actions}<div class="actions">{@render actions()}</div>{/if}
</header>

<style>
  .page-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: var(--space-4) var(--space-6);
    margin-bottom: var(--space-6);
  }
  .heading {
    min-width: 0;
  }
  .eyebrow {
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.14em;
    text-transform: uppercase;
    margin-bottom: var(--space-3);
  }
  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-3xl);
    line-height: var(--leading-tight);
    font-weight: var(--weight-regular);
    letter-spacing: -0.045em;
  }
  .subtitle {
    max-width: 48ch;
    color: var(--text-muted);
    margin-top: var(--space-2);
    font-size: var(--text-sm);
    line-height: var(--leading-relaxed);
  }
  .actions {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-3);
  }
  @media (max-width: 30rem) {
    .actions {
      width: 100%;
    }
    .actions :global(.button) {
      flex: 1 1 auto;
    }
  }
</style>
