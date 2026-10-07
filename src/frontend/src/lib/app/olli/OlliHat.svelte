<script lang="ts">
  /** The chef's hat, tipped by `tilt` degrees about where it sits on the rim. */
  let { tilt }: { tilt: number } = $props();

  const puffs = [
    [45, 29, 12],
    [60, 22, 14.5],
    [75, 29, 12]
  ] as const;
</script>

<g transform="rotate({tilt} 60 47)">
  {#each puffs as [x, y, r] (x)}
    <circle class="hat" cx={x} cy={y} {r} />
  {/each}
  {#each puffs as [x, y, r] (x)}
    <circle class="hat bare" cx={x} cy={y} {r} />
  {/each}
  <rect class="hat" x="35" y="34" width="50" height="13" rx="3" />
  <path class="hat-shade" d="M39 43 h42" />
</g>

<style>
  .hat-shade {
    stroke: var(--mascot-hat-shade);
    stroke-width: 2;
  }
</style>
