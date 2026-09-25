<script lang="ts">
  import { onDestroy } from 'svelte';

  import { TextArea } from '$ds';

  import { createAutosave } from '$features/recipes/editor/autosave.svelte';
  import { m } from '$shell/i18n';
  import { formatDate } from '$shell/i18n';
  import { cookLog } from './stores/cookLog.svelte';
  import AttemptStrip from './AttemptStrip.svelte';
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
    /** Cook mode keeps the note but leaves attempt history on the detail page. */
    variant?: 'detail' | 'cook';
  }

  let { recipeId, variant = 'detail' }: Props = $props();

  const autosave = createAutosave(() => notes.save(recipeId));

  onDestroy(() => {
    void autosave.flush();
    autosave.dispose();
  });

  $effect(() => {
    void notes.load(recipeId);

    if (variant === 'detail') {
      void cookLog.load(recipeId);
    }
  });

  const lastMade = $derived(
    cookLog.lastMadeAt ? formatDate(new Date(cookLog.lastMadeAt), { dateStyle: 'medium' }) : null
  );
</script>

<section class="notes" class:compact={variant === 'cook'} aria-label={m['notes.title']()}>
  {#if variant === 'detail'}
    <header class="head">
      <h2 class="title">{m['notes.title']()}</h2>
      <p class="status" role="status">{autosave.state === 'saved' ? m['notes.saved']() : ''}</p>
    </header>
  {:else}
    <p class="status" role="status">{autosave.state === 'saved' ? m['notes.saved']() : ''}</p>
  {/if}

  <p class="hint">{m['notes.hint']()}</p>

  <!-- Paper only. The note lives in a text area, and a text area prints as an
       empty box; this is the same words in a form paper can carry. -->
  {#if notes.overall}<p class="written">{notes.overall}</p>{/if}

  {#if variant === 'detail'}
    <AttemptStrip {recipeId} />

    {#if cookLog.count > 0}
      <p class="history">
        {m['notes.madeCount']({ count: cookLog.count })}{#if lastMade}
          · {m['notes.lastMade']({ date: lastMade })}{/if}
      </p>
    {/if}
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
  .written {
    display: none;
  }

  .notes {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-top: var(--space-6);
    border-top: 1px solid var(--border);
  }

  .notes.compact {
    padding-top: 0;
    border-top: 0;
  }

  .compact .status:empty {
    display: none;
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

  /*
   * Notes print when there are any — they are the most useful thing on the
   * sheet, being what went wrong last time. An empty one is a heading and an
   * invitation to type, and paper cannot be typed into.
   */
  @media print {
    .notes {
      display: none;
    }

    .notes:has(.written) {
      display: block;
      break-inside: avoid-page;
    }

    .hint,
    .status {
      display: none;
    }

    .written {
      display: block;
      white-space: pre-wrap;
    }
  }
</style>
