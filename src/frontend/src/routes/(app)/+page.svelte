<script lang="ts">
  import { m } from '$shell/i18n';

  // Placeholder. Replaced by the recipe list in the phase 6 beads.
</script>

<svelte:head><title>{m['recipes.title']()}</title></svelte:head>

<div class="page">
  <h1 class="title">{m['recipes.title']()}</h1>
  <p class="tagline">{m['app.tagline']()}</p>
</div>

<style>
  .page {
    max-width: var(--measure);
    margin: 0 auto;
    padding: var(--space-6) var(--space-4);
  }

  .title {
    margin: 0;
    font-size: var(--text-2xl);
  }

  .tagline {
    margin-top: var(--space-2);
    color: var(--text-muted);
  }
</style>
