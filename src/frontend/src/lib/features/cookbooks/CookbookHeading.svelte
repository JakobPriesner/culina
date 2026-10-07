<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';
  import type { CookbookDetail } from './types';

  interface Props {
    /** Null while loading. */
    cookbook: CookbookDetail | null;
    loading: boolean;
    /** A self-filling shelf, whose rules are shown. */
    automatic: boolean;
  }

  let { cookbook, loading, automatic }: Props = $props();
</script>

<div class="heading">
  <p class="eyebrow">{m['cookbooks.title']()}</p>
  {#if !cookbook && loading}
    <Skeleton width="16rem" height="2.25rem" />
    <Skeleton width="24rem" height="1.25rem" />
  {:else}
    <h1 class="title">{cookbook?.name ?? ''}</h1>

    {#if cookbook?.description}
      <p class="subtitle">{cookbook.description}</p>
    {/if}
  {/if}

  {#if automatic && cookbook?.rules}
    <p class="rules">
      <span class="automatic">{m['cookbooks.kind.label']()}</span>
      {[
        ...cookbook.rules.tags,
        ...cookbook.rules.ingredients,
        ...(cookbook.rules.maxMinutes === null
          ? []
          : [m['cookbooks.rules.minutes']({ count: cookbook.rules.maxMinutes })])
      ].join(' · ')}
    </p>
  {/if}
</div>

<style>
  .heading {
    flex: 1 1 24rem;
    min-width: 0;
  }

  .eyebrow {
    color: var(--text-muted);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .title {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.03em;
  }

  .subtitle {
    color: var(--text-muted);
    margin-top: var(--space-1);
    max-width: 42ch;
  }

  .rules {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .automatic {
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-size: var(--text-xs);
    letter-spacing: 0.06em;
    text-transform: uppercase;
  }
</style>
