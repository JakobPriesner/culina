<script lang="ts" module>
  /**
   * The failures an administrator fixes in the assistant settings. Budgets count
   * because an administrator can raise them; a busy provider or an unreadable
   * answer does not, because nothing there would help.
   */
  const fixedInSettings = new Set([
    'assistance.not_configured',
    'assistance.disabled',
    'assistance.model_missing',
    'assistance.unknown_provider',
    'assistance.drawing_not_supported',
    'assistance.unavailable',
    'assistance.rejected',
    'assistance.budget_exhausted',
    'assistance.personal_budget_exhausted'
  ]);
</script>

<script lang="ts">
  import { resolve } from '$app/paths';

  import type { AppError } from '$api';
  import { session } from '$features/auth/session.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  /**
   * Why the assistant did not answer, in the reader's language, and what to do.
   *
   * One component for all four doors — idea, photograph, improve and draw —
   * because the same provider failing the same way must read the same way
   * wherever it was asked.
   *
   * Where the fix is in the assistant settings, an administrator gets a link
   * straight there and everyone else is told who can help. A failure that only
   * says "try again" to somebody whose model was never downloaded sends them
   * round the same loop for ever.
   */
  interface Props {
    error: AppError;
  }

  let { error }: Props = $props();

  const settings = $derived(fixedInSettings.has(error.code));
</script>

<div class="failure" role="alert">
  <p>{explain(error)}</p>

  {#if settings}
    {#if session.user?.isAdmin}
      <a class="settings" href={resolve('/(app)/me/ai')}>{m['assist.failure.settings']()}</a>
    {:else}
      <p class="hint">{m['assist.failure.askAdmin']()}</p>
    {/if}
  {/if}
</div>

<style>
  .failure {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .hint {
    color: var(--text-muted);
  }

  .settings {
    align-self: flex-start;
    color: var(--text);
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
</style>
