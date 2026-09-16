<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Text for a screen reader that the design does not show.
   *
   * The clipping itself lives in `display/clipped.css`, so this and the
   * elements that cannot be wrapped in it share one definition.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<span class="ds-clipped">{@render children()}</span>
