<script lang="ts">
  import { galleryEnabled } from '$shell/gallery';
  import { m } from '$shell/i18n';
  import RecipeExperience from '$features/recipes/preview/RecipeExperience.svelte';
</script>

<svelte:head><title>{m['preview.pageTitle']()}</title></svelte:head>
{#if galleryEnabled}<RecipeExperience />{/if}
