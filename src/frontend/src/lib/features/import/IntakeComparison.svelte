<script lang="ts">
  import DraftWriting from '$features/assistance/DraftWriting.svelte';
  import { m } from '$shell/i18n';
  import IntakeSourceMaterial from './IntakeSourceMaterial.svelte';
  import { running, type IntakeJob } from './intakes.svelte';

  interface Props {
    job: IntakeJob;
  }

  let { job }: Props = $props();
</script>

<div class="comparison">
  <section class="source">
    <h2>{m['import.review.source']()}</h2>
    <IntakeSourceMaterial {job} />
  </section>
  <section class="recipe">
    <h2>{m['import.review.title']()}</h2>
    <DraftWriting draft={job.draft ?? null} writing={running(job)} showProgress={false} />
  </section>
</div>

<style>
  .comparison {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    border-top: 1px solid var(--border);
  }
  .comparison section {
    min-width: 0;
    padding: var(--space-6) var(--space-4);
  }
  .source {
    background: var(--surface-sunken);
    border-radius: var(--radius-md);
  }
  h2 {
    font-size: var(--text-lg);
    font-weight: var(--weight-medium);
    margin-bottom: var(--space-4);
  }

  @media (width < 42rem) {
    .comparison {
      grid-template-columns: 1fr;
    }
  }
</style>
