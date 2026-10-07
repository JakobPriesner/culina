<script lang="ts">
  import type { PoseSpec } from './poses';

  /**
   * What rises from the pot on arrival: steam, a question, a bulb, sleep.
   * Rises 8 units and fades, and is kept when it means something.
   */
  interface Props {
    steam: PoseSpec['steam'];
    /** How far this arrival's steam has risen, from 0 to 1. */
    progress: number;
  }

  let { steam, progress }: Props = $props();

  const stays = $derived(steam === 'question' || steam === 'sleep');
  const opacity = $derived(stays ? progress : Math.sin(Math.min(1, progress) * Math.PI) * 0.85);
  const rise = $derived((1 - progress) * 8 - (stays ? 0 : progress * 6));
  const sparks = [
    [14, 34, 7],
    [104, 22, 8],
    [110, 60, 5],
    [8, 64, 4.5],
    [60, -6, 5]
  ] as const;
</script>

<g {opacity} transform="translate(0 {rise})">
  {#if steam === 'bulb'}
    <g transform="translate(103 23)">
      <path class="bulb thin" d="M-8 0 a8 8 0 1 1 16 0 q0 4 -4 7 v4 h-8 v-4 q-4 -3 -4 -7 Z" />
      <path class="none thin" d="M-3 14 h6 M0 6 v-6 M-3 -1 l3 3 3 -3" />
      <path class="bulb-rays none" d="M0 -15 v-4 M-13 -8 l-3 -2 M13 -8 l3 -2 M-14 4 h-4 M14 4 h4" />
    </g>
  {:else if steam === 'wisp'}
    <path class="steam" d="M98 44 q-6 -8 0 -16 q6 -8 0 -16" />
  {:else if steam === 'question'}
    <text class="question" x="96" y="34">?</text>
  {:else if steam === 'sleep'}
    <text class="sleep" x="96" y="38" font-size="14">z</text>
    <text class="sleep" x="106" y="26" font-size="10">z</text>
  {:else if steam === 'sparks'}
    {#each sparks as [x, y, s], i (i)}
      <path
        class="spark"
        d="M{x} {y - s} Q{x} {y} {x + s} {y} Q{x} {y} {x} {y + s} Q{x} {y} {x -
          s} {y} Q{x} {y} {x} {y - s}Z"
      />
    {/each}
  {/if}
</g>

<style>
  .bulb {
    fill: var(--mascot-cheek);
  }

  .bulb-rays {
    stroke: var(--mascot-cheek);
  }

  .steam {
    fill: none;
    stroke: var(--mascot-steam);
    stroke-width: 2.8;
  }

  .question {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
    font-family: var(--font-editorial);
    font-size: 24px;
  }

  .sleep {
    fill: var(--mascot-line);
    stroke: none;
    font-family: var(--font-editorial);
    font-style: italic;
  }

  .spark {
    fill: var(--mascot-cheek);
    stroke-width: 1.2;
  }
</style>
