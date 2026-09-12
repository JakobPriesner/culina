<script lang="ts">
  import { onDestroy } from 'svelte';

  import { TextArea } from '$ds';

  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import { m } from '$shell/i18n';
  import { formatDate } from '$shell/i18n';
  import { cookLog } from './stores/cookLog.svelte';
  import { notes } from './stores/notes.svelte';

  /**
   * Your own margin, beside somebody else's recipe.
   *
   * The two things that belong here are the same kind of fact: what you thought
   * and what you did. Neither changes the recipe, which is what lets a
   * household share one and still disagree about the sugar.
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

  $effect(() => {
    void notes.load(recipeId);
    void cookLog.load(recipeId);
  });

  const lastMade = $derived(
    cookLog.lastMadeAt ? formatDate(new Date(cookLog.lastMadeAt), { dateStyle: 'medium' }) : null
  );
</script>

<section class="notes" aria-label={m['notes.title']()}>
  <header class="head">
    <h2 class="title">{m['notes.title']()}</h2>
    <p class="status" role="status">{autosave.state === 'saved' ? m['notes.saved']() : ''}</p>
  </header>

  <p class="hint">{m['notes.hint']()}</p>

  {#if cookLog.count > 0}
    <p class="history">
      {m['notes.madeCount']({ count: cookLog.count })}{#if lastMade}
        · {m['notes.lastMade']({ date: lastMade })}{/if}
    </p>
  {/if}

  <TextArea
    id="overall-note"
    value={notes.overall}
    rows={3}
    placeholder={m['notes.placeholder']()}
    oninput={(text) => {
      notes.set(text);
      autosave.touch();
    }}
  />
</section>

<style>
  .notes {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-top: var(--space-6);
    border-top: 1px solid var(--border);
  }

  .head {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .title {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--text-muted);
  }

  .status,
  .hint,
  .history {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
