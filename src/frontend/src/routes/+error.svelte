<script lang="ts">
  import { page } from '$app/state';
  import { resolve } from '$app/paths';
  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import AppShell from '$shell/AppShell.svelte';
  import Brand from '$shell/Brand.svelte';
  import { m } from '$shell/i18n';
  import LocalePicker from '$shell/LocalePicker.svelte';
  import NotFound from '$shell/NotFound.svelte';
  import Page from '$shell/Page.svelte';
  import ThemeToggle from '$shell/ThemeToggle.svelte';

  /**
   * What the router shows when a route cannot: an address the app has no page
   * for, or a load that threw.
   *
   * Inside the shell for somebody who is signed in, so a mistyped link costs
   * them nothing but a tap on the navigation they already know. Anybody else
   * gets the brand and a way to sign in — nothing about what is behind it.
   *
   * No route above this asked who is signed in (an unknown address matches no
   * group, so no guard ran), hence the session is resolved here, and nothing
   * is drawn until it is: a frame that swaps to the shell a moment later is
   * worse than a blank one.
   */
  const resolved = session.resolve();

  const missing = $derived(page.status === 404);
  const inside = $derived(session.status === 'authenticated' && session.activeHouseholdId !== null);
</script>

<svelte:head>
  <!-- The server answers every address with the app and a 200, so this is the
       only place a crawler can learn there is nothing here. -->
  <meta name="robots" content="noindex" />
  <title>{missing ? m['notFound.tab']() : m['error.unexpected.title']()}</title>
</svelte:head>

{#snippet content()}
  {#if missing}
    <NotFound level={1} />
  {:else}
    <ErrorState title={m['error.unexpected.title']()} body={m['error.unexpected.body']()} level={1}>
      {#snippet action()}
        <Button variant="primary" onclick={() => location.reload()}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  {/if}
{/snippet}

{#await resolved then}
  {#if inside}
    <AppShell><Page>{@render content()}</Page></AppShell>
  {:else}
    <div class="frame">
      <header class="header">
        <a class="home" href={resolve('/(app)')}><Brand /></a>
        <div class="preferences"><LocalePicker compact /><ThemeToggle /></div>
      </header>
      <main>{@render content()}</main>
    </div>
  {/if}
{/await}

<style>
  .frame {
    max-width: var(--layout-wide);
    min-height: 100dvh;
    margin-inline: auto;
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
  }

  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    min-height: var(--space-24);
  }

  .home {
    text-decoration: none;
  }

  .preferences {
    display: flex;
    align-items: center;
    gap: var(--space-1);
  }
</style>
