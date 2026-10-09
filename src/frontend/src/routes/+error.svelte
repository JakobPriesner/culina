<script lang="ts">
  import { page } from '$app/state';
  import { m } from '$shell/i18n';

  /**
   * Kept tiny on purpose: SvelteKit loads this node on every navigation, so the shell, Olli and the pickers
   * come in with the page itself, and only when something actually failed.
   */
  const missing = $derived(page.status === 404);
  const title = $derived(missing ? m['notFound.tab']() : m['error.unexpected.title']());
</script>

<svelte:head>
  <!-- The server answers every address with a 200, so this is the only place a crawler learns there is nothing here. -->
  <meta name="robots" content="noindex" />
  <title>{title}</title>
</svelte:head>

<!-- Nothing is drawn while the chunk loads, as nothing was while the session resolved. -->
{#await import('$shell/ErrorPage.svelte') then { default: ErrorPage }}
  <ErrorPage />
{:catch}
  <!-- Offline, the chunk may not be cached: the message still has to show. -->
  <main>
    <h1>{title}</h1>
    {#if !missing}<p>{m['error.unexpected.body']()}</p>{/if}
  </main>
{/await}

<style>
  main {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--space-6) var(--layout-gutter-end) var(--space-6) var(--layout-gutter-start);
  }
</style>
