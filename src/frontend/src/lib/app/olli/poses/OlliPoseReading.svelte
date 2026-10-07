<script lang="ts">
  import { fade } from 'svelte/transition';

  /** Reading: a card held in front of the pot. Drawn in the pot's own frame. */
  let { motion }: { motion: boolean } = $props();
</script>

<g transform="translate(60 98) rotate(-4)" transition:fade={{ duration: motion ? 150 : 0 }}>
  <rect class="paper" x="-17" y="-12" width="34" height="22" rx="2.5" />
  <path class="paper-line" d="M-11 -5 h22 M-11 0 h16 M-11 5 h19" />
</g>
