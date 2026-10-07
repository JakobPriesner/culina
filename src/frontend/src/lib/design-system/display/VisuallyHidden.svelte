<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Text for a screen reader that the design does not show; the clipping is shared via
   * `display/clipped.css`.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<span class="ds-clipped">{@render children()}</span>
