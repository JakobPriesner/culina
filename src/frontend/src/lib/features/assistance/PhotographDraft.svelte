<script lang="ts">
  import { Button, FilePicker } from '$ds';
  import { m } from '$shell/i18n';

  import AssistFailure from './AssistFailure.svelte';
  import DraftWriting from './DraftWriting.svelte';
  import { saysAnything } from './draftToRecipe';
  import { drafts } from './stores/drafts.svelte';

  import type { Draft } from './draftToRecipe';

  /**
   * A cookbook-page photograph typed out, through the shared draft store (the answer is a stream either way); shown as it is read so mismatches with the page are visible at once.
   * Size is checked here too, so a hopeless upload fails before going up the wire.
   */
  interface Props {
    householdId: string;
    language: string;
    onread: (draft: Draft) => void;
    oncancel: () => void;
  }

  let { householdId, language, onread, oncancel }: Props = $props();

  let picker = $state<ReturnType<typeof FilePicker>>();
  let tooLarge = $state(false);

  /** The same ceiling an uploaded recipe photo has. */
  const maxBytes = 10 * 1024 * 1024;

  async function read(file: File): Promise<void> {
    if (file.size > maxBytes) {
      tooLarge = true;

      return;
    }

    tooLarge = false;

    const failure = await drafts.read({ householdId, language, file });

    if (!failure && drafts.draft) {
      onread(drafts.draft);
    }
  }
</script>

<div class="photo">
  <p class="note">{m['assist.photo.note']()}</p>

  <!-- Only while writing or once something arrived: a failure before the first word shows the reason, not an empty box. -->
  {#if drafts.asking || saysAnything(drafts.draft)}
    <DraftWriting draft={drafts.draft} writing={drafts.asking} />
  {/if}

  {#if tooLarge}
    <p class="failure" role="alert">{m['assist.photo.tooLarge']()}</p>
  {:else if drafts.error}
    <AssistFailure error={drafts.error} />
  {/if}

  <div class="actions">
    <Button loading={drafts.asking} onclick={() => picker?.open()}>
      {m['assist.photo.choose']()}
    </Button>
    <Button variant="ghost" onclick={oncancel}>{m['assist.idea.cancel']()}</Button>
  </div>

  <FilePicker
    bind:this={picker}
    label={m['assist.photo.label']()}
    accept="image/jpeg,image/png,image/webp,image/heic"
    onpick={(file) => void read(file)}
  />

  <p class="note">{m['assist.warning']()}</p>
</div>

<style>
  .photo {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .note {
    max-width: var(--measure);
    color: var(--text-muted);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .failure {
    max-width: var(--measure);
    color: var(--text-danger);
    font-size: var(--text-sm);
  }
</style>
