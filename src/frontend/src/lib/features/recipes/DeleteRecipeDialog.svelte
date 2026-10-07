<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Modal } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * The one confirmation Culina asks: delete has no undo (history, notes, photos, planned meals and shelf
   * places go too), so the modal says what goes.
   */
  interface Props {
    open: boolean;
    title: string;
    deleting: boolean;
    /** Why the last try failed; said in here, not a toast, since everything outside an open dialog is inert. */
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
  <p class="body">{m['recipe.delete.restorable']()}</p>

  {#if error}
    <p class="failure" role="alert">
      {m['recipe.delete.failed']()}
      {explain(error)}
    </p>
  {/if}

  {#snippet footer()}
    <!-- The safe answer is the ordinary button, so a reflexive press keeps the recipe. -->
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
