<script lang="ts">
  import { galleryEnabled } from '$shell/gallery';
  import { m } from '$shell/i18n';

  /** Dynamically imported for the same reason as the gallery. See ../+page.svelte. */
</script>

<svelte:head><title>{m['preview.pageTitle']()}</title></svelte:head>

{#if galleryEnabled}
  {#await import('$features/recipes/preview/RecipeExperience.svelte') then module}
    <module.default />
  {/await}
{/if}
