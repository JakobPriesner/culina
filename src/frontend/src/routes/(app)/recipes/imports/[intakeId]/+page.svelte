<script lang="ts">
  import { onMount } from 'svelte';

  import { page } from '$app/state';
  import IntakeComparison from '$features/import/IntakeComparison.svelte';
  import IntakeStatus from '$features/import/IntakeStatus.svelte';
  import { intakes } from '$features/import/intakes.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  const id = $derived(page.params.intakeId ?? '');
  const job = $derived(intakes.jobs.find((job) => job.id === id));

  let loaded = $state(false);

  onMount(() => {
    void intakes.get(id).finally(() => (loaded = true));
  });
</script>

<svelte:head><title>{m['intake.activity']()} · {m['app.name']()}</title></svelte:head>

<div class="intake-page">
  <a class="back" href="/recipes">← {m['intake.back']()}</a>
  {#if job}
    <IntakeStatus {job} />
    <IntakeComparison {job} />
  {:else if loaded}
    <p role="alert">{m['intake.missing']()}</p>
    {#if intakes.error}<p>{explain(intakes.error)}</p>{/if}
  {:else}
    <p role="status">{m['intake.queued']()}</p>
  {/if}
</div>

<style>
  .intake-page {
    max-width: 64rem;
    margin: 0 auto;
    padding: var(--space-4);
  }
  .back {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
