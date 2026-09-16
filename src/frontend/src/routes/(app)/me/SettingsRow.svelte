<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * One setting: what it is called, why it is there, and the control.
   *
   * Written once because five of them were written five ways — a label that was
   * a `span` beside an icon button, a label inside a picker, a bare paragraph
   * with no label at all — and a column of rows that each align differently is
   * the thing that makes a settings screen look unfinished.
   *
   * The label is a `span`, not a `label`, and that is deliberate. A control
   * that takes a value already carries its own label — the pickers keep theirs,
   * clipped — and a second `label` pointing at the same select is two labels on
   * one field, which axe fails and a screen reader reads out twice. What this
   * row contributes is the visible line; the name a screen reader uses comes
   * from the control, and the two say the same words.
   *
   * `group` is the exception: a set of radios cannot be named by a `label` at
   * all, so the row becomes a named `group` and the label the thing that names
   * it. Not a `fieldset` and a `legend`, which only caption when the legend is
   * the fieldset's first child — and here the label sits in a column beside the
   * control rather than above the whole row.
   */
  interface Props {
    children: Snippet;
    label: string;
    /** One line under the label. What it does, or what it deliberately does not. */
    description?: string;
    /** The content is a set of controls that the row's label must name. */
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
    /* Gives the control the rest of the row and lets the text wrap first. */
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

  .control {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
    min-width: 0;
  }

  /* Once the row is narrower than a comfortable label-and-control pair, the
     control takes its own line at full width rather than being squeezed into
     the last few characters of the row. */
  @container (width < 30rem) {
    .control {
      flex: 1 1 100%;
      justify-content: flex-start;
    }
  }
</style>
