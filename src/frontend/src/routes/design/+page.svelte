<script lang="ts">
  import { galleryEnabled } from '$shell/gallery';

  /**
   * The route the gallery lives at, and nothing else.
   *
   * The gallery itself is imported dynamically. That is what makes it truly
   * absent from a release build rather than merely unreachable: with
   * `galleryEnabled` folded to `false`, the bundler removes the import, never
   * emits the chunk, and no specimen markup ships at all. A static import
   * behind an `{#if}` leaves the component's hoisted templates in the bundle
   * even when nothing can reach them — which is how the whole gallery shipped
   * once already.
   */
</script>

{#if galleryEnabled}
  {#await import('./Gallery.svelte') then module}
    <module.default />
  {/await}
{/if}
