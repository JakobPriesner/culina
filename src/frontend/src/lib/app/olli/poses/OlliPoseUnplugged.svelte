<script lang="ts">
  import { fade } from 'svelte/transition';

  /** Unplugged: the plug and its cable beside the pot, which stands in front of them. */
  let { motion }: { motion: boolean } = $props();
</script>

<g transition:fade={{ duration: motion ? 150 : 0 }}>
  <path class="none" d="M92 92 C108 96 110 108 98 110 C88 112 84 106 76 110" />
  <g transform="translate(70 110) rotate(-10)">
    <rect class="plug" x="-10" y="-5" width="12" height="10" rx="2" />
    <path d="M-10 -2.5 h-5 M-10 2.5 h-5" />
  </g>
</g>

<style>
  .plug {
    fill: var(--mascot-paper-line);
  }
</style>
