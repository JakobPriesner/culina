<script lang="ts">
  import { m } from '$shell/i18n';

  import { sourceLink } from '$features/recipes/sourceLink';

  import DraftWriting from './DraftWriting.svelte';
  import type { Draft } from './draftToRecipe';

  /** An intake's source — the text, the transcript, the photos — beside the draft written from it. */
  interface Props {
    source: { text: string; transcript: string; url: string; photos: string[] };
    draft: Draft | null;
    writing: boolean;
  }

  let { source, draft, writing }: Props = $props();

  const original = $derived(sourceLink(source.url));
</script>

<div class="source-comparison">
  <section class="original" aria-label={m['import.review.source']()}>
    <h3>{m['import.review.source']()}</h3>
    {#if original}
      <!-- An external web address, checked by sourceLink, not an application route. -->
      <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
      <a href={original.href} target="_blank" rel="noopener noreferrer"
        >{m['import.review.openSource']()}</a
      >
    {/if}
    {#if source.text}<p class="source-text">{source.text}</p>{/if}
    {#if source.transcript}
      <h4>{m['import.review.transcript']()}</h4>
      <p class="source-text">{source.transcript}</p>
    {/if}
    {#each source.photos as photo (photo)}
      <img src={photo} alt={m['import.review.photo']()} loading="lazy" decoding="async" />
    {/each}
  </section>
  <DraftWriting {draft} {writing} />
</div>

<style>
  .source-comparison {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-4);
    margin-top: var(--space-4);
    align-items: start;
  }

  .original {
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .original h3,
  .original h4 {
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }

  .original img {
    width: 100%;
    height: auto;
    border-radius: var(--radius-md);
  }

  .source-text {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font-size: var(--text-sm);
  }

  @media (max-width: 40rem) {
    .source-comparison {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
