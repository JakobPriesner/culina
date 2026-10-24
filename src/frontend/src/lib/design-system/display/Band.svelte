<script lang="ts">
  /**
   * An honest total against a reference (docs/diary-design.md §9.3): solid ink for what was counted, hatched when it
   * rests partly on estimates, an open dashed end when it is only a lower bound, and a tick for the reference. Past the
   * reference it simply goes on in the same ink: no status colour, no "over". The visible number beside it is the
   * truth; this repeats it for the eye, and says it in full for a screen reader.
   */
  interface Props {
    value: number;
    /** What the band is measured against: a target. The band shows nothing to compare without one. */
    reference: number;
    /** Something was not counted, so the value could be higher. */
    atLeast?: boolean;
    /** Part of the value is an estimate. */
    estimated?: boolean;
    /** The whole sentence a screen reader hears, in place of the drawing. */
    label: string;
  }

  let { value, reference, atLeast = false, estimated = false, label }: Props = $props();

  // Room past the reference, so a day over it still shows where the reference was.
  const scale = $derived(Math.max(reference, value) * 1.1 || 1);
  const filled = $derived(Math.min(1, Math.max(0, value / scale)));
  const tick = $derived(Math.min(1, reference / scale));
</script>

<div class="band" role="img" aria-label={label}>
  <span class="fill" class:estimated style:inline-size="{filled * 100}%"></span>
  {#if atLeast}<span class="open" style:inset-inline-start="{filled * 100}%"></span>{/if}
  <span class="tick" style:inset-inline-start="{tick * 100}%"></span>
</div>

<style>
  .band {
    position: relative;
    block-size: var(--space-2);
    border-radius: var(--radius-full);
    background: var(--surface-sunken);
  }

  .fill {
    position: absolute;
    inset-block: 0;
    inset-inline-start: 0;
    border-radius: inherit;
    background: var(--text-muted);
    transition: inline-size var(--duration-base) var(--ease-out);
  }

  /* An estimate is drawn, not coloured: the same ink, hatched. */
  .estimated {
    background: repeating-linear-gradient(
      -45deg,
      var(--text-muted) 0 3px,
      var(--border-strong) 3px 6px
    );
  }

  /* A lower bound has no end: a dashed run after the fill says "and maybe more". */
  .open {
    position: absolute;
    inset-block: 0;
    inline-size: var(--space-6);
    max-inline-size: calc(100% - var(--space-1));
    border-block: 1px dashed var(--text-subtle);
    border-inline-end: 1px dashed var(--text-subtle);
    border-start-end-radius: var(--radius-full);
    border-end-end-radius: var(--radius-full);
  }

  .tick {
    position: absolute;
    inset-block: calc(var(--space-1) * -1);
    inline-size: 2px;
    margin-inline-start: -1px;
    border-radius: var(--radius-full);
    background: var(--border-strong);
  }

  @media (prefers-reduced-motion: reduce) {
    .fill {
      transition: none;
    }
  }
</style>
