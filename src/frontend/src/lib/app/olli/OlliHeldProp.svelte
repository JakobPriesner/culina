<script lang="ts">
  import { fade } from 'svelte/transition';

  import type { PoseSpec } from './poses';
  import type { Rig } from './rig.svelte';

  /**
   * The props Olli carries: the phone, the pencil and paper, the easel and
   * brush, the card. Drawn inside the pot's own frame, so they lean and squash
   * with it. Styled by Olli, whose rules reach in through `:global`.
   */
  interface Props {
    prop: PoseSpec['prop'];
    motion: boolean;
    rig: Rig;
    /** The right handle's path, which follows the brush to the easel. */
    rightHandle: string;
  }

  let { prop, motion, rig, rightHandle }: Props = $props();

  const paperLines = [0, 1, 2];
</script>

{#if prop === 'phone'}
  <g transform="translate(99 76) rotate(-12)" transition:fade={{ duration: motion ? 180 : 0 }}>
    <rect class="phone" x="-11" y="-20" width="22" height="38" rx="4" />
    <rect class="screen bare" x="-7" y="-14" width="14" height="23" rx="1.5" />
    <path class="play bare" d="M-3 -9 l7 5 -7 5 Z" />
    <path class="paper-line" d="M-2 13 h4" />
  </g>
{:else if prop === 'pencil'}
  <g transform="translate(57 96) rotate(-5)" transition:fade={{ duration: motion ? 180 : 0 }}>
    <rect class="paper thin" x="-22" y="-12" width="45" height="25" rx="2" />
    {#each paperLines as line (line)}
      <path
        class="paper-line"
        d="M-15 {line * 6 - 5} h{Math.min(1, Math.max(0, rig.pencil.current - line)) *
          (line === 2 ? 18 : 29)}"
      />
    {/each}
    <g
      transform="translate({rig.pencilX.current} {rig.pencilY.current -
        rig.penLift.current}) rotate(27)"
    >
      <path class="pencil thin" d="M0 0 l-2 -7 v-20 h5 v20 Z" />
      <path class="ink bare" d="M0 0 l-1 -3 h3 Z" />
      <path class="hat thin" d="M-2 -27 v-4 q2.5 -3 5 0 v4 Z" />
    </g>
  </g>
{:else if prop === 'brush'}
  <g class="palette" transform="translate(34 96) rotate(-12)">
    <ellipse class="paper thin" rx="14" ry="8" />
    <ellipse class="pot thin" cx="7" cy="1" rx="3" ry="2.5" />
    <circle class="paint-food bare" cx="-7" cy="-1" r="2.5" />
    <circle class="pencil bare" cx="-1" cy="-3" r="2.5" />
  </g>
  <g class="easel" transition:fade={{ duration: motion ? 180 : 0 }}>
    <path class="none" d="M91 103 l-3 8 M108 103 l5 8 M101 102 v9" />
    <rect class="paper" x="86" y="65" width="29" height="36" rx="2" />
    <path class="paper-line" d="M84 102 h33" />
    <g class="painting">
      <ellipse
        class="paint-plate thin"
        cx="101"
        cy="86"
        rx="10"
        ry="6"
        opacity={Math.min(1, rig.paint.current)}
      />
      <path
        class="paint-food thin"
        d="M94 86 q0 -8 6 -7 q7 -3 8 5 q-4 5 -14 2 Z"
        opacity={Math.min(1, Math.max(0, rig.paint.current - 1))}
      />
      <path
        class="paint-detail none"
        d="M97 81 l3 3 M103 80 l-2 5 M106 85 l-3 2"
        opacity={Math.min(1, Math.max(0, rig.paint.current - 2))}
      />
    </g>
  </g>
  <g class="handle handle-right"><path class="rim" d={rightHandle} /></g>
  <g class="brush" transform="translate({rig.brushX.current} {rig.brushY.current}) rotate(32)">
    <path class="pencil thin" d="M-2 -26 h4 v20 h-4 Z" />
    <path class="hat thin" d="M-2 -6 h4 v4 h-4 Z" />
    <path class="paint-food thin" class:warm={rig.warmBrush} d="M-2 -2 q-3 3 2 6 q5 -3 2 -6 Z" />
  </g>
{/if}

{#if prop === 'card'}
  <g transform="translate(60 98) rotate(-4)" transition:fade={{ duration: motion ? 150 : 0 }}>
    <rect class="paper" x="-17" y="-12" width="34" height="22" rx="2.5" />
    <path class="paper-line" d="M-11 -5 h22 M-11 0 h16 M-11 5 h19" />
  </g>
{/if}
