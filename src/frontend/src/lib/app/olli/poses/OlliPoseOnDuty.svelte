<script lang="ts">
  import { fade } from 'svelte/transition';

  /** On duty: the order ticket beside the pot, which stands in front of it. */
  let { motion }: { motion: boolean } = $props();
</script>

<g transform="translate(103 80) rotate(8)" transition:fade={{ duration: motion ? 150 : 0 }}>
  <rect class="paper thin" x="-12" y="-20" width="24" height="34" />
  <path class="paper-line" d="M-7 -12 h14 M-7 -6 h10 M-7 0 h14" />
  <path class="ticket-line" d="M-7 7 h14" />
</g>

<style>
  .ticket-line {
    stroke: var(--text-danger);
    stroke-width: 2.4;
  }
</style>
