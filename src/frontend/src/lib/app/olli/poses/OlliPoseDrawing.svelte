<script lang="ts">
  import { fade } from 'svelte/transition';

  import { brushGrip, rightHandlePath } from '../geometry';
  import type { Rig } from '../rig.svelte';

  // The right handle follows the brush, so it is drawn here, over the easel.
  let { motion, rig }: { motion: boolean; rig: Rig } = $props();

  const rightHandle = $derived(
    rightHandlePath(true, brushGrip(rig.brushX.current, rig.brushY.current))
  );
</script>

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

<style>
  .paint-plate {
    fill: var(--mascot-hat);
  }

  .paint-food.warm {
    fill: var(--mascot-cheek);
  }

  .paint-food {
    fill: var(--mascot-rim);
  }

  .paint-detail {
    stroke: var(--mascot-cheek);
  }
</style>
