<script lang="ts">
  import { m } from '$shell/i18n';

  // Replaced by the list itself in the phase 8 beads.
</script>

<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<div class="page">
  <h1>{m['shopping.title']()}</h1>
  <p class="note">{m['shopping.comingSoon']()}</p>
</div>

<style>
  .page {
    max-width: var(--measure);
    margin: 0 auto;
    padding: var(--space-6) var(--space-4);
  }

  .note {
    margin-top: var(--space-2);
    color: var(--text-muted);
  }
</style>
