<script lang="ts">
  import { selectionIndicator } from '../containment/selectionIndicator';
  /**
   * Two or three views of the same content, all named at once; not `Tabs` (which own a panel), so it can sit beside a heading.
   * Pressed buttons, not radios: choosing is immediate. Every segment stays in the tab order, as a roving tabindex would hide the other option.
   */
  export interface Segment {
    readonly id: string;
    readonly label: string;
  }

  interface Props {
    segments: readonly Segment[];
    selected: string;
    /** Names the control, since the segments alone do not say what they switch. */
    label: string;
    onselect: (id: string) => void;
  }

  let { segments, selected, label, onselect }: Props = $props();
</script>

<div use:selectionIndicator={{ selected }} class="segments" role="group" aria-label={label}>
  {#each segments as segment (segment.id)}
    <button
      class="segment"
      data-selection={segment.id}
      type="button"
      aria-pressed={selected === segment.id}
      onclick={() => onselect(segment.id)}
    >
      {segment.label}
    </button>
  {/each}
</div>

<style>
  /* One control with a seam: the shared border says the segments are alternatives. */
  .segments {
    position: relative;
    isolation: isolate;
    display: inline-flex;
    padding: var(--space-1);
    gap: var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .segment {
    position: relative;
    z-index: 1;
    padding: var(--space-1) var(--space-2);
    min-height: var(--control-sm);
    border: none;
    border-radius: var(--radius-full);
    background: none;
    color: var(--text-muted);
    font: inherit;
    font-size: var(--text-xs);
    font-weight: var(--weight-medium);
    white-space: nowrap;
    cursor: pointer;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      color var(--duration-fast) var(--ease-out);
  }

  .segment:hover {
    color: var(--text);
  }

  /* Raised, not accented: an accent-coloured control asks to be pressed as if something would happen. */
  .segment[aria-pressed='true'] {
    color: var(--text);
  }

  .segments:not([data-indicator-ready]) .segment[aria-pressed='true'] {
    background: var(--surface-raised);
    color: var(--text);
    box-shadow: var(--shadow-card);
  }

  /* Paper has one arrangement, whichever was on screen. */
  @media print {
    .segments {
      display: none;
    }
  }
</style>
