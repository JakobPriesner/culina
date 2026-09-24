<script lang="ts">
  import '../app.css';

  import { onMount, type Snippet } from 'svelte';

  import { dev } from '$app/environment';
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { handleSessionExpiry } from '$api';
  import { ErrorState } from '$ds';
  import { loginUrlFor } from '$features/auth/redirectTarget';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { connection } from '$shell/connection.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import { watchForUpdates } from '$shell/updates.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  onMount(() => {
    // The static boot screen in app.html has done its job the moment there is
    // something real to look at.
    document.getElementById('boot')?.remove();

    const stopFollowingTheDevice = preferences.start();
    // Not under the dev server: every rebuild there is a "new version", and a
    // prompt to reload after each save is noise the production app never has.
    const stopWatchingForUpdates = dev ? () => {} : watchForUpdates();
    const stopFollowingTheNetwork = connection.start();

    // The API layer decides *when* a session has ended; what happens next is
    // the app's business, and keeping that here is what stops the client from
    // having to know that a router exists.
    handleSessionExpiry(() => {
      session.end();
      void goto(loginUrlFor(page.url), { replaceState: true });
    });

    return () => {
      stopFollowingTheDevice();
      stopWatchingForUpdates();
      stopFollowingTheNetwork();
    };
  });
</script>

<!--
  Re-creating the tree is what makes a language switch take effect without a
  reload: compiled messages are plain function calls, so nothing else would tell
  Svelte that every string on the page just changed. Language changes are rare
  enough that the cost never shows.
-->
{#key preferences.locale}
  <!--
    The last line of defence. A component that throws would otherwise leave a
    blank page with no way forward; this keeps something on screen that says
    what happened and offers a way out.
  -->
  <svelte:boundary>
    {@render children()}

    {#snippet failed(_error, reset)}
      <div class="boundary">
        <ErrorState
          title={m['error.unexpected.title']()}
          body={m['error.unexpected.body']()}
          level={1}
        >
          {#snippet action()}
            <button class="retry" type="button" onclick={reset}>{m['error.retry']()}</button>
          {/snippet}
        </ErrorState>
      </div>
    {/snippet}
  </svelte:boundary>
{/key}

<style>
  .boundary {
    display: grid;
    place-items: center;
    min-height: 100dvh;
  }

  /*
   * A plain button rather than the design system's: whatever threw might have
   * been inside it, and the one control that recovers the app must not depend
   * on the part that is broken.
   */
  .retry {
    min-height: var(--control-md);
    padding-inline: var(--space-4);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }
</style>
