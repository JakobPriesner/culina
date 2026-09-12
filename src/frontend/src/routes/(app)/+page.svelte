<script lang="ts">
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  // Placeholder. Replaced by the recipe list in the phase 6 beads.
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<Page>
  <h1 class="title">{m['recipes.title']()}</h1>
  <p class="tagline">{m['app.tagline']()}</p>
</Page>

<style>
  .title {
    margin: 0;
    font-size: var(--text-2xl);
  }

  .tagline {
    margin-top: var(--space-2);
    color: var(--text-muted);
  }
</style>
