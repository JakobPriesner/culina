<script lang="ts" module>
  /** Failures an administrator can fix in the assistant settings: budgets count, a busy provider or unreadable answer doesn't. */
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
   * Why the assistant did not answer, and what to do; one component for all four doors so the same failure
   * reads the same. An administrator gets a settings link, everyone else is told who can help.
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
