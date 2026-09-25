<script lang="ts">
  import { Button, Sheet, Skeleton } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';
  import { sharing } from './stores/sharing.svelte';

  /**
   * Handing one recipe to somebody who does not have Culina.
   *
   * Opening the sheet is the decision to share: the link is made as it opens,
   * so the address is on screen without a second tap, beside the sentence that
   * says anyone holding it can read the recipe. The sheet is also where the
   * link is read back and where it is taken away again — the one place that
   * answers "is this recipe out there?".
   *
   * The link is deliberately re-readable, unlike a household invitation, which
   * is shown once. See the sharing store for why.
   */
  interface Props {
    open: boolean;
    recipeId: string;
    /** For the sentence the native share sheet shows beside the address. */
    title: string;
    onclose: () => void;
  }

  let { open, recipeId, title, onclose }: Props = $props();

  // Only while it is open: a closed sheet that keeps its answer warm is one
  // more request on every recipe page nobody asked for.
  $effect(() => {
    if (open) {
      void share();
    }
  });

  const token = $derived(sharing.tokenFor(recipeId));

  /**
   * Built here and not by the server.
   *
   * Which origin Culina is reached at is a fact the browser holds — an instance
   * behind a proxy or on a second hostname would otherwise hand out links to
   * the wrong one.
   */
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

  /**
   * The phone's own share sheet when there is one, the clipboard otherwise.
   *
   * `navigator.share` is what "send this to someone" means on a phone: it opens
   * the list of people and apps the reader already uses, which no button here
   * could reproduce. On a desktop it usually does not exist, and there copying
   * is what sharing is.
   */
  async function send() {
    if (!link) {
      return;
    }

    if (navigator.share) {
      try {
        await navigator.share({ title, url: link });

        return;
      } catch {
        // Dismissing the system sheet throws, and that is not a failure —
        // somebody changed their mind. Fall through to the clipboard, which
        // also covers a browser that has the method but refuses the call.
      }
    }

    try {
      await navigator.clipboard.writeText(link);
      toaster.show({ message: () => m['recipe.share.copied'](), tone: 'success' });
    } catch {
      // A browser that refuses the clipboard is not worth a message: the link
      // is on screen and can be selected.
    }
  }
</script>

<Sheet {open} title={m['recipe.share.title']()} closeLabel={m['recipe.share.done']()} {onclose}>
  <div class="panel">
    {#if link}
      <p class="lead">{m['recipe.share.on']()}</p>

      <!-- Readable and selectable, not a field: there is nothing to type here,
           and an input invites somebody to edit an address. -->
      <p class="link" data-testid="share-link">{link}</p>

      <div class="actions">
        <Button variant="primary" onclick={send}>{m['recipe.share.send']()}</Button>
        <Button variant="ghost" onclick={revoke} loading={sharing.working}>
          {m['recipe.share.revoke']()}
        </Button>
      </div>

      <p class="note">{m['recipe.share.revokeHint']()}</p>
    {:else if sharing.status === 'loading'}
      <!-- The shape of the shared state, so the link lands where it is drawn. -->
      <Skeleton width="100%" height="1rem" />
      <Skeleton width="100%" height="2.5rem" />
      <Skeleton width="10rem" height="2.5rem" />
    {:else}
      <!-- Only after the link was taken back, or could not be made. -->
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

  /* Monospaced and sunken: an address somebody may want to check character by
     character before sending it to the wrong chat. */
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
