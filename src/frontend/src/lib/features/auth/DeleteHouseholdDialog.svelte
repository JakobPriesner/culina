<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Modal } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /** The question before a household is deleted; it names who else is affected (everyone is shut out), ordinary answer first. */
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
  title={m['household.delete.title']({ name })}
  closeLabel={m['household.delete.close']()}
  {onclose}
>
  <p class="body">{m['household.delete.body']()}</p>
  <p class="body">{m['household.delete.restorable']()}</p>

  {#if error}
    <p class="failure" role="alert">
      {m['household.delete.failed']()}
      {explain(error)}
    </p>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['household.delete.cancel']()}</Button>
    <Button variant="danger" loading={deleting} onclick={onconfirm}>
      {m['household.delete.confirm']()}
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

  .body + .body {
    margin-top: var(--space-2);
  }

  .failure {
    margin-top: var(--space-3);
    color: var(--text-danger);
  }
</style>
