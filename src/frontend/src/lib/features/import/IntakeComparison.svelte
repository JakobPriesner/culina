<script lang="ts">
  import DraftWriting from '$features/assistance/DraftWriting.svelte';
  import { sourceLink } from '$features/recipes/sourceLink';
  import { m } from '$shell/i18n';
  import { running, type IntakeJob } from './intakes.svelte';

  interface Props {
    job: IntakeJob;
  }

  let { job }: Props = $props();

  /** A link only for a web address: what was shared is not this app's to vouch for. */
  const original = $derived(sourceLink(job.sourceUrl));
</script>

<div class="comparison">
  <section class="source">
    <h2>{m['import.review.source']()}</h2>
    {#if original}<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
      <a class="source-link" href={original.href} target="_blank" rel="noopener noreferrer"
        >{original.href}</a
      >{/if}
    {#if job.material}<pre>{job.material}</pre>{/if}
    {#if job.transcript}<h3>{m['import.review.transcript']()}</h3>
      <pre>{job.transcript}</pre>{/if}
    {#if job.photoCount}<div class="photos">
        {#each Array(job.photoCount) as _, index (index)}<img
            src="/api/v1/recipe-intakes/{job.id}/photos/{index}"
            alt={m['import.review.photo']()}
            loading="lazy"
            decoding="async"
          />{/each}
      </div>{/if}
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
  h3 {
    margin-block: var(--space-3);
    font-size: var(--text-sm);
  }
  pre {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font: inherit;
    font-size: var(--text-sm);
    line-height: 1.65;
    margin-block: var(--space-3);
  }
  .source-link {
    overflow-wrap: anywhere;
    font-size: var(--text-sm);
  }
  .photos {
    display: grid;
    gap: var(--space-3);
  }
  .photos img {
    width: 100%;
    height: auto;
    border-radius: var(--radius-md);
  }

  @media (width < 42rem) {
    .comparison {
      grid-template-columns: 1fr;
    }
  }
</style>
