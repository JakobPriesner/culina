<script lang="ts">
  import { Button, FilePicker } from '$ds';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';

  import DraftWriting from './DraftWriting.svelte';
  import { drafts } from './stores/drafts.svelte';

  import type { Draft } from './draftToRecipe';

  /**
   * A photograph of a cookbook page, typed out.
   *
   * Through the shared draft store like the other three. It used not to be,
   * because a photograph goes up as multipart where the others send JSON and
   * one generated client method could not bind both — but the answer is a
   * stream either way now, and "what to do while a draft arrives in pieces" is
   * worth having exactly one copy of.
   *
   * The recipe is shown as it is read, which matters more here than anywhere
   * else in the feature: this is the one capability where the answer can be
   * checked against something. A line that does not match the page is visible
   * the moment it is written rather than at the end of the wait.
   *
   * The size is checked here as well as on the server, so an obviously hopeless
   * upload fails instantly rather than after the photo has gone up the wire.
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

  {#if drafts.asking || drafts.draft}
    <DraftWriting draft={drafts.draft} writing={drafts.asking} />
  {/if}

  {#if tooLarge}
    <p class="failure" role="alert">{m['assist.photo.tooLarge']()}</p>
  {:else if drafts.error}
    <p class="failure" role="alert">{explain(drafts.error)}</p>
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
