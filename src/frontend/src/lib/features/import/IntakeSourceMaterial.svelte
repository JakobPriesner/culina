<script lang="ts">
  import { sourced } from '$ds/display/imageSources';
  import { sourceLink } from '$features/recipes/sourceLink';
  import { m } from '$shell/i18n';
  import type { IntakeJob } from './intakes.svelte';

  /** What an import was read from: the address, the words and the photos, as they were sent. */
  interface Props {
    job: IntakeJob;
  }

  let { job }: Props = $props();

  /** A link only for a web address: what was shared is not this app's to vouch for. */
  const original = $derived(sourceLink(job.sourceUrl));
</script>

{#if original}<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
  <a class="source-link" href={original.href} target="_blank" rel="noopener noreferrer"
    >{original.href}</a
  >{/if}
{#if job.material}<pre>{job.material}</pre>{/if}
{#if job.transcript}<h3>{m['import.review.transcript']()}</h3>
  <pre>{job.transcript}</pre>{/if}
{#if job.photoCount}<div class="photos">
    {#each Array(job.photoCount) as _, index (index)}<img
        {@attach sourced(`/api/v1/recipe-intakes/${job.id}/photos/${index}`)}
        alt={m['import.review.photo']()}
        loading="lazy"
        decoding="async"
      />{/each}
  </div>{/if}

<style>
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
</style>
