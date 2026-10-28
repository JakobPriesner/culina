<script lang="ts">
  import { SectionLabel } from '$ds';

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
    {#if eyebrow}<SectionLabel tone="accent" spacing="loose">{eyebrow}</SectionLabel>{/if}
    <h1>{title}</h1>
    {#if subtitle}<p class="subtitle">{subtitle}</p>{/if}
  </div>
  {#if actions}<div class="actions">{@render actions()}</div>{/if}
</header>

<style>
  /*
   * The page's one action stays at the end of the title's line at every width, never a full-width bar of its own. The
   * title starts from nothing and grows, so only a group that cannot fit beside a short title (several tools, or
   * enlarged text) wraps under it.
   */
  .page-header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-start;
    justify-content: space-between;
    gap: var(--space-4) var(--space-6);
    margin-bottom: var(--space-6);
  }
  .heading {
    flex: 1 1 0;
    min-width: min(100%, 8rem);
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
  /* As tall as the title's first line, so the action centres on it whatever sits below. */
  .actions {
    display: flex;
    flex-shrink: 0;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--space-3);
    min-height: calc(var(--text-3xl) * var(--leading-tight));
  }
</style>
