<script lang="ts">
  import { onDestroy, tick } from 'svelte';

  import { page } from '$app/state';
  import { Button, TextArea } from '$ds';

  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import { m } from '$shell/i18n';
  import { notes } from './stores/notes.svelte';

  /**
   * Your note, near the top of the recipe, and the one place it is read and written on this page: seen
   * before shopping or starting, so it is not left at the foot (where it was a second copy). Saved as
   * typed; `#overall-note` (the wrap-up's "note for next time") opens it for writing.
   */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();

  const autosave = createAutosave(() => notes.save(recipeId));

  onDestroy(() => {
    void autosave.flush();
    autosave.dispose();
  });

  let writing = $state(false);

  const note = $derived(notes.loaded ? notes.overall.trim() : '');

  async function write() {
    writing = true;
    await tick();
    document.getElementById('overall-note')?.focus();
  }

  async function done() {
    writing = false;
    await autosave.flush();
  }

  $effect(() => {
    if (page.url.hash === '#overall-note' && notes.loaded) {
      void write();
    }
  });
</script>

{#if notes.loaded}
  {#if writing}
    <div class="peek">
      <TextArea
        id="overall-note"
        label={m['notes.title']()}
        value={notes.overall}
        rows={3}
        placeholder={m['notes.placeholder']()}
        oninput={(text) => {
          notes.set(text);
          autosave.touch();
        }}
      />
      <p class="hint">{m['notes.hint']()}</p>
      <div class="row">
        <Button size="sm" onclick={() => void done()}>{m['notes.peek.done']()}</Button>
        <p class="hint" role="status">{autosave.state === 'saved' ? m['notes.saved']() : ''}</p>
      </div>
    </div>
  {:else if note}
    <aside class="peek" aria-label={m['notes.title']()}>
      <p class="note">{note}</p>
      <button type="button" class="edit" onclick={() => void write()}>
        {m['notes.peek.edit']()}
      </button>
    </aside>
  {:else}
    <button type="button" class="add" onclick={() => void write()}>
      + {m['notes.peek.add']()}
    </button>
  {/if}
{/if}

<style>
  .peek {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    margin-top: var(--space-3);
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-sm);
    background: var(--surface-sunken);
    font-size: var(--text-sm);
  }

  .peek > :global(*:first-child) {
    align-self: stretch;
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

  .hint {
    margin: 0;
    color: var(--text-muted);
  }

  .row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .edit,
  .add {
    padding: 0;
    border: 0;
    background: none;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
  }

  .add {
    margin-top: var(--space-3);
    font-size: var(--text-sm);
  }

  /* The note itself prints at the foot of the page. */
  @media print {
    .peek,
    .add {
      display: none;
    }
  }
</style>
