<script lang="ts">
  import { m } from '$shell/i18n';
  import { notes } from './stores/notes.svelte';

  /**
   * Your note, near the top of the recipe: written so the next cook sees it before shopping or starting, so it is not left
   * at the foot of the page. Read-only here; the link goes to where it is edited.
   */
  const note = $derived(notes.loaded ? notes.overall.trim() : '');
</script>

{#if note}
  <aside class="peek" aria-label={m['notes.title']()}>
    <p class="note">{note}</p>
    <a class="edit" href="#overall-note">{m['notes.peek.edit']()}</a>
  </aside>
{/if}

<style>
  .peek {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    margin-top: var(--space-3);
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .note {
    margin: 0;
    white-space: pre-wrap;
    /* A long note stays a glance; the whole of it is a tap away. */
    display: -webkit-box;
    -webkit-line-clamp: 4;
    line-clamp: 4;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  .edit {
    align-self: flex-start;
    color: var(--accent);
    font-weight: var(--weight-medium);
  }

  /* The note itself prints at the foot of the page. */
  @media print {
    .peek {
      display: none;
    }
  }
</style>
