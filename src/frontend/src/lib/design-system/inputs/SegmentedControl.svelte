<script lang="ts">
  /**
   * Two or three ways of looking at the same thing, all named at once.
   *
   * Not `Tabs`: tabs own a panel and say "the rest of this page is about the
   * one you picked". This says "here is the same content, arranged
   * differently", so it can sit beside a heading and leave the page alone.
   *
   * Pressed buttons rather than radios, because choosing takes effect
   * immediately and there is no form to submit. Both segments stay in the tab
   * order: with two of them, a roving tabindex would save one key press and
   * cost the reader the ability to see the other option is there.
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

<div class="segments" role="group" aria-label={label}>
  {#each segments as segment (segment.id)}
    <button
      class="segment"
      type="button"
      aria-pressed={selected === segment.id}
      onclick={() => onselect(segment.id)}
    >
      {segment.label}
    </button>
  {/each}
</div>

<style>
  /* One control with a seam down it, not two buttons that happen to touch:
     the shared border is what says the two are alternatives. */
  .segments {
    display: inline-flex;
    padding: var(--space-1);
    gap: var(--space-1);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .segment {
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

  /* Raised, not accented: this picks a view, and a control painted in the
     accent colour asks to be pressed as if something were about to happen. */
  .segment[aria-pressed='true'] {
    background: var(--surface-raised);
    color: var(--text);
    box-shadow: var(--shadow-card);
  }

  /* Paper has one arrangement — whichever was on screen — and no way to change
     it. */
  @media print {
    .segments {
      display: none;
    }
  }
</style>
