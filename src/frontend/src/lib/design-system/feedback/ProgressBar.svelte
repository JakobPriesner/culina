<script lang="ts">
  /**
   * How far along something is.
   *
   * Only for work with a known end — an upload, a step counter. Anything else
   * should use `BusyRegion`, because a bar that fills to an arbitrary point and
   * waits is a lie about progress.
   */
  interface Props {
    value: number;
    max?: number;
    /** Names the bar. Required: "63%" of what is not a question a reader can answer. */
    label: string;
    /** Spoken in place of the raw number, e.g. "Step 3 of 8". */
    valueText?: string;
  }

  let { value, max = 100, label, valueText }: Props = $props();

  const fraction = $derived(max > 0 ? Math.min(1, Math.max(0, value / max)) : 0);
</script>

<div
  class="track"
  role="progressbar"
  aria-label={label}
  aria-valuenow={value}
  aria-valuemin={0}
  aria-valuemax={max}
  aria-valuetext={valueText}
>
  <span class="fill" style:transform="scaleX({fraction})"></span>
</div>

<style>
  .track {
    height: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
    overflow: hidden;
  }

  .fill {
    display: block;
    height: 100%;
    border-radius: inherit;
    background: var(--accent);
    /* Scaled rather than resized: a transform does not cause layout, so a bar
       updating many times a second stays cheap. */
    transform-origin: left center;
    transition: transform var(--duration-base) var(--ease-out);
  }
</style>
