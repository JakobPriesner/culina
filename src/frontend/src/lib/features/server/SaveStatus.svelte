<script lang="ts">
  import { Button } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import type { SaveOutcome, SavePhase } from './stores/server.svelte';

  /**
   * What saving a server setting did, said under the button that did it.
   *
   * A restart takes the server away for a moment, and a screen that says
   * nothing while it is gone looks broken. So each phase has its sentence, and
   * a restart that does not come back says where to look and how to undo it,
   * rather than spinning forever.
   *
   * For the two database failures, the server's own words follow the
   * translated headline: they carry the part only the server knows — the
   * password that was refused, the extension that is missing and the
   * statement that adds it.
   */
  interface Props {
    phase: SavePhase;
    outcome: SaveOutcome | null;
    onretry: () => void;
  }

  let { phase, outcome, onretry }: Props = $props();

  const withDetail = new Set(['settings.database_unreachable', 'settings.database_unsuitable']);
</script>

{#if phase === 'restarting'}
  <p class="state" role="status">{m['server.restarting']()}</p>
{:else if outcome?.kind === 'unchanged'}
  <p class="state" role="status">{m['server.unchanged']()}</p>
{:else if outcome?.kind === 'applied'}
  <p class="state" role="status">{m['server.applied']()}</p>
{:else if outcome?.kind === 'stalled'}
  <div class="failure" role="alert">
    <p>{m['server.stalled']()}</p>
    <Button variant="secondary" size="sm" onclick={onretry}>{m['server.stalled.retry']()}</Button>
  </div>
{:else if outcome?.kind === 'failed'}
  <div class="failure" role="alert">
    <p>{explain(outcome.error)}</p>
    {#if withDetail.has(outcome.error.code)}
      <p class="detail">{outcome.error.detail}</p>
    {/if}
    {#each outcome.error.fields as cause (cause.detail)}
      <p class="detail">{cause.detail}</p>
    {/each}
  </div>
{/if}

<style>
  .state {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failure {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  /* The server's sentence can end in a SQL statement somebody has to copy. */
  .detail {
    overflow-wrap: anywhere;
    color: var(--text);
  }
</style>
