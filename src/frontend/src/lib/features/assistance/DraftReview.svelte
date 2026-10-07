<script lang="ts">
  import { Button, Modal } from '$ds';
  import type { AppError } from '$api';
  import { m } from '$shell/i18n';
  import { explain } from '$shell/explain';

  import AssistFailure from './AssistFailure.svelte';
  import DraftParts from './DraftParts.svelte';
  import DraftPending from './DraftPending.svelte';
  import { draftParts } from './draftParts';
  import SourceComparison from './SourceComparison.svelte';
  import {
    acceptEverything,
    acceptNothing,
    anyAccepted,
    toPatch,
    type Accepted,
    type Draft
  } from './draftToRecipe';

  import type { Recipe } from '$features/recipes/types';

  /**
   * What the assistant suggested beside what is there now; nothing touches the draft until a person ticks something and presses the button (the editor autosaves ~800 ms after any change).
   * Six rows, not one per ingredient (steps naming vanished ingredients make no sense), all unticked, opening on the first thing said; nothing is acceptable until the draft is finished.
   */
  interface Props {
    open: boolean;
    draft: Draft | null;
    current: Recipe;
    /** Whether more of the draft is still arriving. */
    writing?: boolean;
    saving?: boolean;
    saveError?: AppError | null;
    /** Why the assistant stopped part-way; what it wrote is still offered, and the reason is said here where the reader is. */
    error?: AppError | null;
    /** Source comparison for an intake draft; no recipe exists yet. */
    source?: { text: string; transcript: string; url: string; photos: string[]; assisted: boolean };
    onaccept: (patch: Partial<Recipe>) => void;
    onclose: () => void;
  }

  let {
    open = $bindable(),
    draft,
    current,
    writing = false,
    saving = false,
    saveError = null,
    error = null,
    source,
    onaccept,
    onclose
  }: Props = $props();

  let accepted = $state<Accepted>(acceptNothing());

  const parts = $derived(draftParts(draft, current));

  function accept(): void {
    if (!draft) {
      return;
    }

    onaccept(toPatch(draft, accepted, current));
    accepted = acceptNothing();
  }

  function close(): void {
    accepted = acceptNothing();
    onclose();
  }
</script>

<Modal
  bind:open
  wide={!!source}
  title={source ? m['import.review.title']() : m['assist.improve.title']()}
  closeLabel={m['assist.close']()}
  onclose={close}
>
  {#if source}
    <p class="lead">{m['import.review.hint']()}</p>
    <SourceComparison {source} {draft} {writing} />
  {:else}
    <p class="lead">{m['assist.improve.lead']()}</p>

    {#if writing}
      <DraftPending arriving={draft !== null} />
    {/if}

    {#if draft || !writing}
      {#if parts.length === 0 && !writing && !error}
        <p class="lead">{m['assist.nothing']()}</p>
      {:else if draft}
        <DraftParts
          {parts}
          {accepted}
          steps={draft.steps}
          onchange={(key, checked) => (accepted = { ...accepted, [key]: checked })}
        />
      {/if}
    {/if}
  {/if}

  {#if saveError}<p class="warning" role="alert">{explain(saveError)}</p>{/if}

  {#if error}
    <AssistFailure {error} />
  {/if}

  <!-- Said here, not only on the button: this is when somebody decides whether to trust it. -->
  {#if draft && (!source || source.assisted)}
    <p class="warning">{m['assist.warning']()}</p>
  {/if}

  {#snippet footer()}
    <Button variant="ghost" onclick={close}>{m['assist.discard']()}</Button>

    {#if source && draft && parts.length > 0}
      <Button
        variant="primary"
        loading={saving}
        disabled={writing || saving || !!error}
        onclick={() => onaccept(toPatch(draft, acceptEverything(draft), current))}
      >
        {m['import.review.accept']()}
      </Button>
    {:else if draft && parts.length > 0}
      <Button
        variant="secondary"
        disabled={writing}
        onclick={() => (accepted = acceptEverything(draft))}
      >
        {m['assist.acceptAll']()}
      </Button>
      <Button onclick={accept} disabled={writing || !anyAccepted(accepted)}>
        {m['assist.accept']()}
      </Button>
    {/if}
  {/snippet}
</Modal>

<style>
  .lead {
    max-width: var(--measure);
    color: var(--text-muted);
    line-height: var(--leading-normal);
  }

  .warning {
    max-width: var(--measure);
    margin-top: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
