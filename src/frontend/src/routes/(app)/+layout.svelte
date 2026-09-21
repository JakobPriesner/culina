<script lang="ts">
  import type { Snippet } from 'svelte';

  import { onMount } from 'svelte';

  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import AppShell from '$shell/AppShell.svelte';
  import { m } from '$shell/i18n';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  /**
   * The session could not be read, which the guard deliberately does not treat
   * as being signed out. Nothing behind here can render without it, so this is
   * the one screen that replaces the whole shell rather than sitting inside it.
   */
  const unreachable = $derived(session.status === 'unavailable');

  let retrying = $state(false);

  // Asked once, on boot: the answer drives the bar that leads back to whatever
  // is on the hob, and it is needed on every page rather than one.
  onMount(() => {
    if (!unreachable) {
      void cooking.resume();
    }
  });

  async function retry() {
    retrying = true;

    try {
      await session.refresh();

      if (session.status === 'authenticated') {
        void cooking.resume();
      }
    } finally {
      retrying = false;
    }
  }
</script>

{#if unreachable}
  <div class="unreachable">
    <ErrorState
      title={m['session.unavailable.title']()}
      body={m['session.unavailable.body']()}
      level={1}
    >
      {#snippet action()}
        <Button variant="primary" onclick={retry} loading={retrying}>{m['error.retry']()}</Button>
      {/snippet}
    </ErrorState>
  </div>
{:else}
  <AppShell>{@render children()}</AppShell>
{/if}

<style>
  .unreachable {
    display: grid;
    place-items: center;
    min-height: 100dvh;
  }
</style>
