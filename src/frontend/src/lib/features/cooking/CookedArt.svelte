<script lang="ts" module>
  import Olli from '$shell/olli/Olli.svelte';

  export { cookedArt };
</script>

<!-- Olli celebrating, for the "made it" toast wherever cooking is recorded from. -->
{#snippet cookedArt()}
  <Olli pose="celebrating" size="sm" still />
{/snippet}
