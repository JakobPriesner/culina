<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Modal } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * The one question Culina asks before doing something.
   *
   * Everywhere else it does the thing and offers Undo. Deleting a recipe has no
   * undo to offer: the server removes it outright, and its cooking history,
   * notes, photos, planned meals and shelf places go with it. Putting a recipe
   * back would be writing a new one that has none of that. So this is the rare
   * case the modal exists for, and it says plainly what goes.
   */
  interface Props {
    open: boolean;
    title: string;
    /** The delete is in flight. */
    deleting: boolean;
    /**
     * Why the last try failed. Said in here, not in a toast: while the dialog
     * is open everything outside it is inert, a toast included.
     */
    error: AppError | null;
    onconfirm: () => void;
    onclose: () => void;
  }

  let { open, title, deleting, error, onconfirm, onclose }: Props = $props();
</script>

<Modal
  {open}
  title={m['recipe.delete.title']({ title })}
  closeLabel={m['recipe.delete.close']()}
  {onclose}
>
  <p class="body">{m['recipe.delete.body']()}</p>
  <p class="body">{m['recipe.delete.permanent']()}</p>

  {#if error}
    <p class="failure" role="alert">
      {m['recipe.delete.failed']()}
      {explain(error)}
    </p>
  {/if}

  {#snippet footer()}
    <!-- The safe answer is the ordinary button, so a reflexive press of the
         familiar one keeps the recipe. -->
    <Button onclick={onclose}>{m['recipe.delete.cancel']()}</Button>
    <Button variant="danger" loading={deleting} onclick={onconfirm}>
      {m['recipe.delete.confirm']()}
    </Button>
  {/snippet}
</Modal>

<style>
  .body {
    max-width: var(--measure);
    color: var(--text-muted);
  }

  .body + .body,
  .failure {
    margin-top: var(--space-3);
  }

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
  }
</style>
