<script lang="ts">
  import { fade } from 'svelte/transition';

  import type { Rig } from '../rig.svelte';

  /** Writing: paper on the pot's lap and a pencil that fills it line by line. */
  let { motion, rig }: { motion: boolean; rig: Rig } = $props();

  const paperLines = [0, 1, 2];
</script>

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
