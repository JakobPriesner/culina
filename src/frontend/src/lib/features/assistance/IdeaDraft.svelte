<script lang="ts">
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

  const ready = $derived(idea.trim().length > 0);

  async function ask(): Promise<void> {
    if (!ready) {
      return;
    }

    const failure = await drafts.ask({
      kind: 'idea',
      householdId,
      material: idea.trim(),
      language
    });

    if (!failure && drafts.draft) {
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
  {#if drafts.asking || saysAnything(drafts.draft)}
    <DraftWriting draft={drafts.draft} writing={drafts.asking} />
  {/if}

  {#if drafts.error}
    <AssistFailure error={drafts.error} />
  {/if}

  <div class="actions">
    <Button onclick={ask} loading={drafts.asking} disabled={!ready}>
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
