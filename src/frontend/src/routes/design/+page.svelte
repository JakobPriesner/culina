<script lang="ts">
  import { galleryEnabled } from '$shell/gallery';

  /** Only the route: the gallery is imported dynamically so it is truly absent from release builds (a static import behind `{#if}` leaves hoisted templates in the bundle). */
</script>

{#if galleryEnabled}
  {#await import('./Gallery.svelte') then module}
    <module.default />
  {/await}
{/if}
