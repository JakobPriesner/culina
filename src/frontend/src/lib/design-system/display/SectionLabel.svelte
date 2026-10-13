<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * The small heading over a section, a fieldset or a row: sentence case, semibold, 15px (docs/design-system.md,
   * Labels), in one of three tones so a page can say how much it matters. It was restated in every component that
   * needed one, and the copies drifted in size and colour.
   */
  interface Props {
    children: Snippet;
    /**
     * `accent` names the page or form a title belongs to (the eyebrow above a heading), `muted` heads a section,
     * `subtle` marks a legend or a row inside one.
     */
    tone?: 'accent' | 'muted' | 'subtle';
    /** The element it is: a legend for a fieldset, a heading where the outline needs one, a term in a list. */
    as?: 'p' | 'span' | 'div' | 'h2' | 'h3' | 'h4' | 'legend' | 'dt';
    /** Room below it, for a label that sits on top of what it names rather than in a gap the parent sets. */
    spacing?: 'none' | 'tight' | 'loose';
    id?: string;
  }

  let { children, tone = 'subtle', as = 'p', spacing = 'none', id }: Props = $props();
</script>

<svelte:element this={as} {id} class="section-label {tone} {spacing}"
  >{@render children()}</svelte:element
>

<style>
  .section-label {
    margin: 0;
    padding: 0;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .accent {
    color: var(--accent);
  }

  .muted {
    color: var(--text-muted);
  }

  .subtle {
    color: var(--text-subtle);
  }

  .tight {
    margin-bottom: var(--space-2);
  }

  .loose {
    margin-bottom: var(--space-3);
  }
</style>
