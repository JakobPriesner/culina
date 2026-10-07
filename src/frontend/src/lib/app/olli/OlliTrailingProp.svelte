<script lang="ts">
  import { fade } from 'svelte/transition';

  import type { PoseSpec } from './poses';

  /**
   * The props that sit beside the pot rather than in its hands: the plug, and
   * the ticket. Drawn before the body, so the pot stands in front of them.
   * Styled by Olli, whose rules reach in through `:global`.
   */
  let { prop, motion }: { prop: PoseSpec['prop']; motion: boolean } = $props();
</script>

{#if prop === 'plug'}
  <g transition:fade={{ duration: motion ? 150 : 0 }}>
    <path class="none" d="M92 92 C108 96 110 108 98 110 C88 112 84 106 76 110" />
    <g transform="translate(70 110) rotate(-10)">
      <rect class="plug" x="-10" y="-5" width="12" height="10" rx="2" />
      <path d="M-10 -2.5 h-5 M-10 2.5 h-5" />
    </g>
  </g>
{:else if prop === 'ticket'}
  <g transform="translate(103 80) rotate(8)" transition:fade={{ duration: motion ? 150 : 0 }}>
    <rect class="paper thin" x="-12" y="-20" width="24" height="34" />
    <path class="paper-line" d="M-7 -12 h14 M-7 -6 h10 M-7 0 h14" />
    <path class="ticket-line" d="M-7 7 h14" />
  </g>
{/if}
