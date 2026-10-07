<script lang="ts">
  import { fade } from 'svelte/transition';

  /** Watching: a phone held in the right handle. Drawn in the pot's own frame. */
  let { motion }: { motion: boolean } = $props();
</script>

<g transform="translate(99 76) rotate(-12)" transition:fade={{ duration: motion ? 180 : 0 }}>
  <rect class="phone" x="-11" y="-20" width="22" height="38" rx="4" />
  <rect class="screen bare" x="-7" y="-14" width="14" height="23" rx="1.5" />
  <path class="play bare" d="M-3 -9 l7 5 -7 5 Z" />
  <path class="paper-line" d="M-2 13 h4" />
</g>

<style>
  .phone {
    fill: var(--mascot-line);
  }

  .screen {
    fill: var(--mascot-hat);
  }

  .play {
    fill: var(--mascot-cheek);
  }
</style>
