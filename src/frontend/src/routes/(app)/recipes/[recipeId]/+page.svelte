<script lang="ts">
  import { page } from '$app/state';

  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  // A placeholder so the list has somewhere to link to. Replaced by
  // RecipeSurface — the component the product is built around — in the bead
  // that follows this one.
  const recipeId = $derived(page.params.recipeId ?? '');
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<Page width="reading">
  <h1>{m['recipes.title']()}</h1>
  <p class="id">{recipeId}</p>
</Page>

<style>
  .id {
    margin-top: var(--space-2);
    color: var(--text-muted);
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: var(--text-sm);
  }
</style>
