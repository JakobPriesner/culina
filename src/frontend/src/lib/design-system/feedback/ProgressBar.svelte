<script lang="ts">
  /** How far along something is; only for work with a known end, otherwise use `BusyRegion`. */
  interface Props {
    value: number;
    max?: number;
    /** Names the bar. Required: "63%" of what is not a question a reader can answer. */
    label: string;
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
    /* Scaled, not resized: a transform causes no layout, so frequent updates stay cheap. */
    transform-origin: left center;
    transition: transform var(--duration-base) var(--ease-out);
  }
</style>
