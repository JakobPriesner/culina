<script lang="ts">
  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';

  // Replaced by the list itself in the phase 8 beads.
</script>

<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<Page>
  <h1>{m['shopping.title']()}</h1>
  <p class="note">{m['shopping.comingSoon']()}</p>
</Page>

<style>
  .note {
    margin-top: var(--space-2);
    color: var(--text-muted);
  }
</style>
