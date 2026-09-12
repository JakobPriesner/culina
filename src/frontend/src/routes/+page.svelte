<script lang="ts">
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  // Placeholder shell. Replaced by the recipe list in the phase 6 beads; the
  // controls live here until the app shell exists to hold them.
</script>

<svelte:head><title>{m['app.name']()}</title></svelte:head>

<main class="page">
  <header class="bar">
    <h1 class="title">{m['app.name']()}</h1>
    <div class="controls">
      <LocalePicker />
      <ThemeToggle />
    </div>
  </header>

  <p class="tagline">{m['app.tagline']()}</p>
</main>

<style>
  .page {
    max-width: var(--measure);
    margin: 0 auto;
    padding: var(--space-6) var(--space-4);
  }

  .bar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .title {
    margin: 0;
    font-size: var(--text-2xl);
  }

  .controls {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .tagline {
    margin-top: var(--space-2);
    color: var(--text-muted);
  }
</style>
