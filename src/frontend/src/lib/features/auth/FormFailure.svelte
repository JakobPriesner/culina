<script lang="ts">
  import type { AppError } from '$api';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * What went wrong with the form as a whole.
   *
   * Only shown for a failure that is not about a single field — those belong on
   * the field itself. `role="alert"` because the submit that caused it has just
   * moved focus expectations, and a message nobody is told about is a message
   * nobody reads.
   */
  interface Props {
    failure: AppError | null;
    /** Overrides the server's prose where the app has something better to say. */
    message?: string;
  }

  let { failure, message }: Props = $props();

  const aboutOneField = $derived(
    (failure?.fields.length ?? 0) > 0 && failure!.fields.every((cause) => cause.field)
  );
</script>

{#if failure && !aboutOneField}
  <div class="failure" role="alert">
    <p class="message">{message ?? explain(failure)}</p>

    {#if failure.requestId}
      <p class="reference">{m['error.reference']()} <code>{failure.requestId}</code></p>
    {/if}
  </div>
{/if}

<style>
  .failure {
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--danger);
    border-radius: var(--radius-md);
    background: var(--danger-subtle);
  }

  .message {
    font-size: var(--text-sm);
  }

  .reference {
    margin-top: var(--space-1);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  code {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    user-select: all;
  }
</style>
