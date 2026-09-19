<script lang="ts">
  import { Button, Field, TextArea } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import { drafts } from './stores/drafts.svelte';

  import type { Draft } from './draftToRecipe';

  /**
   * A sentence about dinner, turned into a first draft.
   *
   * Inline on the page that starts a recipe rather than in a dialog, beside the
   * paste box that works the same way — because it is the same act. Somebody
   * arrives wanting to write a recipe down and has three ways to begin, and a
   * modal would make this one feel like a different part of the app.
   *
   * It stops at the draft. What comes back is handed up as a normal recipe to
   * create, which means it travels the same road as a pasted one: create with
   * a title, then update with the contents. A create-with-everything endpoint
   * would be a second way to write a recipe, and the second way is the one
   * that drifts.
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

  {#if drafts.error}
    <p class="failure" role="alert">{explain(drafts.error)}</p>
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

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .warning {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
