<script lang="ts">
  import '../app.css';

  import { onMount, type Snippet } from 'svelte';

  import { dev } from '$app/environment';
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { handleSessionExpiry } from '$api';
  import ErrorState from '$ds/feedback/ErrorState.svelte';
  import { loginUrlFor } from '$features/auth/redirectTarget';
  import { session } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { appIcon } from '$shell/appIcon.svelte';
  import { connection } from '$shell/connection.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import { report } from '$shell/telemetry';
  import { watchForUpdates } from '$shell/updates.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  onMount(() => {
    // The static boot screen in app.html is done once there is something real to see.
    document.getElementById('boot')?.remove();

    const stopFollowingTheDevice = preferences.start();
    appIcon.start();
    // Not under the dev server, where every rebuild would prompt a reload.
    const stopWatchingForUpdates = dev ? () => {} : watchForUpdates();
    const stopFollowingTheNetwork = connection.start();

    // Ask for persistent storage so the browser does not evict cached recipes and offline shopping data.
    if (typeof navigator !== 'undefined' && navigator.storage?.persist) {
      void navigator.storage.persist();
    }

    // The API layer decides when a session has ended; what happens next is the app's business, so the client need not know a router exists.
    handleSessionExpiry(() => {
      session.end();
      void goto(loginUrlFor(page.url, 'expired'), { replaceState: true });
    });

    return () => {
      stopFollowingTheDevice();
      stopWatchingForUpdates();
      stopFollowingTheNetwork();
    };
  });
</script>

<!-- Re-creating the tree makes a language switch take effect without a reload: compiled messages are plain calls Svelte cannot track. -->
{#key preferences.locale}
  <!-- Last line of defence: keeps something on screen with a way out, and reports it, since a boundary that catches an error is why nothing else hears of it. -->
  <svelte:boundary onerror={(error) => report('render_failed', error)}>
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

  /* A plain button, not the design system's: whatever threw might have been inside it, and the control that recovers the app must not depend on the broken part. */
  .retry {
    min-height: var(--control-md);
    padding-inline: var(--space-4);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-full);
    background: var(--surface-raised);
    color: var(--text);
    font: inherit;
    cursor: pointer;
  }
</style>
