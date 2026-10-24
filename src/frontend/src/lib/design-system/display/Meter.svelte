<script lang="ts">
  /**
   * One amount against one reference, drawn as a single thin track (docs/food-log-redesign.md §4.4, after Tufte and
   * Apple's chart guidance: one mark, the message said in words). The fill is the accent, the reference a short mark;
   * past the reference the fill runs on in the same hue, never a warning colour. How sure the amount is belongs to the
   * sentence beside it, not to the drawing. The label is that sentence, for everyone who does not see the track.
   */
  interface Props {
    value: number;
    /** What the amount is measured against: a target. Without one there is nothing to draw. */
    reference: number;
    /** The sentence a screen reader hears in place of the track. */
    label: string;
    /** `thin` for a secondary amount beside a larger one. */
    size?: 'regular' | 'thin';
  }

  let { value, reference, label, size = 'regular' }: Props = $props();

  // The track is the larger of the two, so a day past its reference still shows where the reference was.
  const scale = $derived(Math.max(reference, value) || 1);
  const filled = $derived(Math.min(1, Math.max(0, value / scale)));
  const mark = $derived(Math.min(1, reference / scale));
</script>

<div class="meter {size}" role="img" aria-label={label}>
  <span class="fill" style:inline-size="{filled * 100}%"></span>
  <span class="mark" style:inset-inline-start="{mark * 100}%"></span>
</div>

<style>
  .meter {
    position: relative;
    block-size: 0.375rem;
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .thin {
    block-size: 0.25rem;
  }

  .fill {
    position: absolute;
    inset-block: 0;
    inset-inline-start: 0;
    border-radius: inherit;
    background: var(--accent);
    transition: inline-size var(--duration-base) var(--ease-out);
  }

  /* The reference: a short upright stroke through the track, in ink rather than the accent. */
  .mark {
    position: absolute;
    inset-block: calc(var(--space-1) * -1);
    inline-size: 2px;
    margin-inline-start: -1px;
    border-radius: var(--radius-full);
    background: var(--text);
  }
</style>
