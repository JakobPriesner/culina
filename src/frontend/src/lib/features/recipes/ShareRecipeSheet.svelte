<script lang="ts">
  import { Button, Sheet, Skeleton } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { sharing } from './stores/sharing.svelte';

  /**
   * Sharing one recipe: the link is made as the sheet opens, and the sheet is where it is read back and revoked.
   * Re-readable, unlike a household invitation (see the sharing store).
   */
  interface Props {
    open: boolean;
    recipeId: string;
    title: string;
    onclose: () => void;
  }

  let { open, recipeId, title, onclose }: Props = $props();

  // Only while open: a closed sheet keeping its answer warm is an extra request per recipe page.
  $effect(() => {
    if (open) {
      void share();
    }
  });

  const token = $derived(sharing.tokenFor(recipeId));

  /** Built client-side: the browser knows which origin Culina is reached at (proxy, second hostname). */
  const link = $derived(token ? `${location.origin}/shared/${token}` : null);

  async function share() {
    const failure = await sharing.share(recipeId);

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });
    }
  }

  async function revoke() {
    const failure = await sharing.revoke(recipeId);

    toaster.show({
      message: () => (failure ? explain(failure) : m['recipe.share.revoked']()),
      tone: failure ? 'danger' : 'neutral'
    });
  }

  /** The native share sheet where there is one, the clipboard otherwise. */
  async function send() {
    if (!link) {
      return;
    }

    if (navigator.share) {
      try {
        await navigator.share({ title, url: link });

        return;
      } catch {
        // Dismissing the system sheet throws, which is not a failure; also covers a browser that refuses the call.
      }
    }

    try {
      await navigator.clipboard.writeText(link);
      toaster.show({ message: () => m['recipe.share.copied'](), tone: 'success' });
    } catch {
      // A refused clipboard is not worth a message: the link is on screen.
    }
  }
</script>

<Sheet {open} title={m['recipe.share.title']()} closeLabel={m['recipe.share.done']()} {onclose}>
  <div class="panel">
    {#if link}
      <p class="lead">{m['recipe.share.on']()}</p>

      <!-- Selectable text, not an input, which would invite editing the address. -->
      <p class="link" data-testid="share-link">{link}</p>

      <div class="actions">
        <Button variant="primary" onclick={send}>{m['recipe.share.send']()}</Button>
        <Button variant="ghost" onclick={revoke} loading={sharing.working}>
          {m['recipe.share.revoke']()}
        </Button>
      </div>

      <p class="note">{m['recipe.share.revokeHint']()}</p>
    {:else if sharing.status === 'loading'}
      <Skeleton width="100%" height="1rem" />
      <Skeleton width="100%" height="2.5rem" />
      <Skeleton width="10rem" height="2.5rem" />
    {:else}
      <p class="lead">{m['recipe.share.off']()}</p>

      <Button variant="primary" onclick={share} loading={sharing.working}>
        {m['recipe.share.create']()}
      </Button>
    {/if}
  </div>
</Sheet>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    align-items: flex-start;
    min-width: 0;
    max-width: 100%;
  }

  .lead {
    max-width: var(--measure);
    color: var(--text-muted);
  }

  /* Monospaced and sunken so the address can be checked character by character. */
  .link {
    width: 100%;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
    overflow-wrap: anywhere;
    font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-subtle);
    font-size: var(--text-sm);
  }
</style>
