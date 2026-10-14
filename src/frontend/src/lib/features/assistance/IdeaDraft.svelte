<script lang="ts">
  import { onDestroy, onMount } from 'svelte';

  import type { AppError } from '$api';
  import { Button, Field, TextArea } from '$ds';
  import { m } from '$shell/i18n';

  import AssistFailure from './AssistFailure.svelte';
  import DraftWriting from './DraftWriting.svelte';
  import { saysAnything } from './draftToRecipe';
  import { drafts } from './stores/drafts.svelte';

  import type { Draft } from './draftToRecipe';

  /**
   * A sentence about dinner turned into a first draft, inline beside the paste box (same act, so no dialog); shown as it is written so it can be rejected early.
   * It stops at the draft and goes the same create-then-update road as a pasted recipe; a create-with-everything endpoint would be a second way that drifts.
   */
  interface Props {
    householdId: string;
    language: string;
    onwritten: (draft: Draft) => void;
    oncancel: () => void;
  }

  let { householdId, language, onwritten, oncancel }: Props = $props();

  let idea = $state('');

  /** Gone once the page is left: a draft finishing later must not start a recipe from a page nobody is on. */
  let here = true;

  const ready = $derived(idea.trim().length > 0);

  /** Only an idea's draft: the store is shared with the editor's rewrite. */
  const mine = $derived(drafts.concerns('idea'));

  onMount(() => {
    // Back after the app was put away or closed: the idea asked for here is picked up again, words and all.
    const resumed = drafts.resume('idea');

    if (resumed) {
      void written(resumed);
    }
  });

  onDestroy(() => {
    here = false;
  });

  $effect(() => {
    if (mine && !idea && drafts.material) {
      idea = drafts.material;
    }
  });

  async function ask(): Promise<void> {
    if (!ready) {
      return;
    }

    await written(
      drafts.ask({
        kind: 'idea',
        householdId,
        material: idea.trim(),
        language
      })
    );
  }

  async function written(asked: Promise<AppError | null>): Promise<void> {
    const failure = await asked;

    if (here && !failure && drafts.concerns('idea') && drafts.draft) {
      onwritten(drafts.draft);
    }
  }
</script>

<div class="idea">
  <Field label={m['assist.idea.label']()}>
    {#snippet children({ id, describedBy, invalid })}
      <TextArea
        {id}
        {describedBy}
        {invalid}
        rows={3}
        placeholder={m['assist.idea.placeholder']()}
        bind:value={idea}
      />
    {/snippet}
  </Field>

  <!-- Only while being written or once something has arrived: a failure before a word leaves the reason, not an empty box. -->
  {#if mine && (drafts.asking || saysAnything(drafts.draft))}
    <DraftWriting draft={drafts.draft} writing={drafts.asking} />
  {/if}

  {#if mine && drafts.error}
    <AssistFailure error={drafts.error} />
  {/if}

  <div class="actions">
    <Button onclick={ask} loading={mine && drafts.asking} disabled={!ready}>
      {m['assist.idea.write']()}
    </Button>
    <Button variant="ghost" onclick={oncancel}>{m['assist.idea.cancel']()}</Button>
  </div>

  <!-- Said before it is asked for, not after it arrives. -->
  <p class="warning">{m['assist.warning']()}</p>
</div>

<style>
  .idea {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .warning {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
