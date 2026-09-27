<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Modal } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  interface Props {
    open: boolean;
    name: string;
    deleting: boolean;
    error: AppError | null;
    onconfirm: () => void;
    onclose: () => void;
  }

  let { open, name, deleting, error, onconfirm, onclose }: Props = $props();
</script>

<Modal
  {open}
  title={m['cookbooks.delete.title']({ name })}
  closeLabel={m['cookbooks.delete.close']()}
  {onclose}
>
  <p class="body">{m['cookbooks.delete.body']()}</p>

  {#if error}
    <p class="failure" role="alert">
      {m['cookbooks.delete.failed']()}
      {explain(error)}
    </p>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['cookbooks.delete.cancel']()}</Button>
    <Button variant="danger" loading={deleting} onclick={onconfirm}>
      {m['cookbooks.delete.action']()}
    </Button>
  {/snippet}
</Modal>

<style>
  .body,
  .failure {
    max-width: var(--measure);
  }

  .body {
    color: var(--text-muted);
  }

  .failure {
    margin-top: var(--space-3);
    color: var(--text-danger);
  }
</style>
