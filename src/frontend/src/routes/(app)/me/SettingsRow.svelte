<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One setting: label, why it is there, and the control, written once so rows align.
   * The label is a `span`: the control carries its own label, and a second `label` is two labels on one field (axe fails it).
   * `group` makes the row a named `group` for radio sets, which a `label` cannot name (and a `legend` only captions as first child).
   */
  interface Props {
    children: Snippet;
    label: string;
    description?: string;
    group?: boolean;
  }

  let { children, label, description, group = false }: Props = $props();

  const labelId = $props.id();
</script>

<div class="row" role={group ? 'group' : undefined} aria-labelledby={group ? labelId : undefined}>
  <div class="text">
    <span class="label" id={labelId}>{label}</span>

    {#if description}
      <p class="description">{description}</p>
    {/if}
  </div>

  <div class="control">{@render children()}</div>
</div>

<style>
  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-6);
    min-width: 0;
    padding: var(--space-4);
  }

  .text {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
    flex: 1 1 14rem;
  }

  .label {
    color: var(--text);
    font-size: var(--text-base);
    font-weight: var(--weight-medium);
  }

  .description {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  /* Wraps rather than squeezing: side-by-side pickers were too narrow to read on a phone. */
  .control {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
    min-width: 0;
  }

  /* Below a comfortable pair width the control takes its own full-width line. */
  @container (width < 30rem) {
    .control {
      flex: 1 1 100%;
      justify-content: flex-start;
    }
  }
</style>
