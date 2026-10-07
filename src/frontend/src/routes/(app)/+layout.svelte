<script lang="ts">
  import type { Snippet } from 'svelte';

  import { onMount } from 'svelte';

  import { Button, ErrorState } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { cooking } from '$features/cooking/stores/cooking.svelte';
  import AppShell from '$shell/AppShell.svelte';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  /** The session could not be read, which the guard does not treat as signed out; nothing renders without it, so this replaces the whole shell. */
  const unreachable = $derived(session.status === 'unavailable');

  let retrying = $state(false);

  // Asked once on boot: the bar leading back to what is on the hob is needed on every page.
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
      {#snippet art()}<Olli pose="dozing" />{/snippet}
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
