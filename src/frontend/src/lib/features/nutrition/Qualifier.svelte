<script lang="ts">
  import { nbsp, qualifierOf } from './format';

  /**
   * The word before a figure that is not exact ("mind.", "ca."; see `qualifierOf`), muted beside the number.
   * Nothing at all when the value is exact, so it can sit before every number.
   */
  let { atLeast = false, estimated = false }: { atLeast?: boolean; estimated?: boolean } = $props();

  const word = $derived(qualifierOf({ atLeast, estimated }));
</script>

{#if word}<span class="qualifier">{word}{nbsp}</span>{/if}

<style>
  .qualifier {
    color: var(--text-muted);
    font-weight: var(--weight-regular);
  }
</style>
