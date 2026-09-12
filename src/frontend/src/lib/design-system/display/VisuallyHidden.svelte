<script lang="ts">
  import type { Snippet } from 'svelte';

  /**
   * Text for a screen reader that the design does not show.
   *
   * Clipped rather than `display: none` or `visibility: hidden`, both of which
   * remove it from the accessibility tree as well — which is the opposite of
   * what this is for.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();
</script>

<span class="hidden">{@render children()}</span>

<style>
  .hidden {
    position: absolute;
    width: 1px;
    height: 1px;
    margin: -1px;
    padding: 0;
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
    border: 0;
  }
</style>
