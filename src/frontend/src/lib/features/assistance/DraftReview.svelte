<script lang="ts">
  import { Button, Checkbox, GenerationAura, Modal, Skeleton } from '$ds';
  import type { AppError } from '$api';
  import { m } from '$shell/i18n';
  import { explain } from '$shell/explain';

  import AssistFailure from './AssistFailure.svelte';
  import DraftProgress from './DraftProgress.svelte';
  import DraftWriting from './DraftWriting.svelte';
  import {
    acceptEverything,
    acceptNothing,
    anyAccepted,
    offers,
    toPatch,
    type Accepted,
    type Draft
  } from './draftToRecipe';

  import type { Recipe } from '$features/recipes/types';

  /**
   * What the assistant suggested, beside what is there now.
   *
   * The whole reason this capability is safe to offer. An assistant that
   * rewrote somebody's recipe and saved it would be one nobody could trust with
   * the recipe they actually cook from — and the editor has no Save button, so
   * anything that reached `change()` would be on its way to the server 800 ms
   * later. Nothing here touches the draft until a person has ticked something
   * and pressed the button.
   *
   * Six rows rather than one per ingredient, which looks like less control and
   * is more. Steps that came from the assistant over an ingredient list that
   * did not are steps naming things the recipe no longer has; the list is the
   * smallest piece that still makes sense on its own. Correcting one line
   * afterwards is what the editor underneath is for.
   *
   * Everything starts unticked. A dialog that opens with its work already
   * accepted is a dialog people dismiss without reading.
   *
   * It opens on the first thing the assistant says rather than on the last, so
   * the suggestion is read as it is written. Nothing can be accepted until it
   * is finished: ticking a box against half an ingredient list and pressing the
   * button would apply a list the assistant had not finished writing — and this
   * editor has no Save button, so that would be on its way to the server 800 ms
   * later.
   */
  interface Props {
    open: boolean;
    draft: Draft | null;
    current: Recipe;
    /** Whether more of the draft is still arriving. */
    writing?: boolean;
    saving?: boolean;
    saveError?: AppError | null;
    /**
     * Why the assistant stopped, when it stopped part-way.
     *
     * What it wrote before then is still offered — it was paid for — but the
     * reason is said here, where the reader is, rather than only beside a
     * button the dialog is covering.
     */
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

  const available = $derived(draft ? offers(draft) : acceptNothing());
  const parts = $derived.by(() => {
    if (!draft) {
      return [];
    }

    return (
      [
        ['title', m['assist.part.title'](), current.title, draft.title],
        ['description', m['assist.part.description'](), current.description, draft.description],
        ['yield', m['assist.part.yield'](), describeYield(current), describeDraftYield()],
        ['times', m['assist.part.times'](), describeTimes(current), describeDraftTimes()],
        [
          'ingredients',
          m['assist.part.ingredients'](),
          m['assist.lines']({ count: current.groups.flatMap((g) => g.ingredients).length }),
          m['assist.lines']({ count: draft.groups.flatMap((g) => g.ingredients).length })
        ],
        [
          'steps',
          m['assist.part.steps'](),
          m['assist.stepCount']({ count: current.steps.length }),
          m['assist.stepCount']({ count: draft.steps.length })
        ]
      ] as const
    ).filter(([key]) => available[key]);
  });

  function describeYield(recipe: Recipe): string {
    return `${recipe.yieldAmount} ${recipe.yieldLabel ?? ''}`.trim();
  }

  function describeDraftYield(): string {
    if (!draft) {
      return '';
    }

    return `${draft.yieldAmount ?? current.yieldAmount} ${draft.yieldLabel ?? ''}`.trim();
  }

  function describeTimes(recipe: Recipe): string {
    return [recipe.prepMinutes, recipe.cookMinutes]
      .filter((value): value is number => value != null)
      .map((value) => m['assist.minutes']({ count: value }))
      .join(' + ');
  }

  function describeDraftTimes(): string {
    if (!draft) {
      return '';
    }

    return [draft.prepMinutes, draft.cookMinutes]
      .filter((value): value is number => value != null)
      .map((value) => m['assist.minutes']({ count: value }))
      .join(' + ');
  }

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
    <div class="source-comparison">
      <section class="original" aria-label={m['import.review.source']()}>
        <h3>{m['import.review.source']()}</h3>
        {#if source.url}
          <!-- External URL validated by intake, not an application route. -->
          <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
          <a href={source.url} target="_blank" rel="noopener noreferrer"
            >{m['import.review.openSource']()}</a
          >
        {/if}
        {#if source.text}<p class="source-text">{source.text}</p>{/if}
        {#if source.transcript}
          <h4>{m['import.review.transcript']()}</h4>
          <p class="source-text">{source.transcript}</p>
        {/if}
        {#each source.photos as photo (photo)}
          <img src={photo} alt={m['import.review.photo']()} />
        {/each}
      </section>
      <DraftWriting {draft} {writing} />
    </div>
  {:else}
    <p class="lead">{m['assist.improve.lead']()}</p>

    {#if writing}
      <div class="progress">
        <GenerationAura />
        <DraftProgress
          label={draft ? m['assist.improve.writing']() : m['assist.improve.asking']()}
          arriving={draft !== null}
        />
      </div>
    {/if}

    {#if !draft && writing}
      <div class="forming" aria-hidden="true">
        <Skeleton width="9rem" height="1rem" />
        <Skeleton width="100%" height="3.5rem" shape="block" />
        <Skeleton width="7rem" height="1rem" />
        <Skeleton width="82%" height="1rem" />
      </div>
    {:else if parts.length === 0 && !writing && !error}
      <p class="lead">{m['assist.nothing']()}</p>
    {:else if draft}
      <ul class="parts">
        {#each parts as [key, label, before, after] (key)}
          <li class="part arrival">
            <Checkbox
              checked={accepted[key]}
              {label}
              onchange={(checked) => (accepted = { ...accepted, [key]: checked })}
            />

            <div class="compare">
              <p class="side">
                <span class="which">{m['assist.before']()}</span>
                <span class="was">{before || m['assist.empty']()}</span>
              </p>
              <p class="side">
                <span class="which">{m['assist.after']()}</span>
                <span>{after || m['assist.empty']()}</span>
              </p>
            </div>
          </li>
        {/each}
      </ul>

      {#if draft.steps.length > 0}
        <ol class="steps">
          {#each draft.steps as step, index (index)}
            <li class="arrival">
              {#if step.title}<span class="stepTitle">{step.title}</span>{/if}
              {step.text}
            </li>
          {/each}
        </ol>
      {/if}
    {/if}
  {/if}

  {#if saveError}<p class="warning" role="alert">{explain(saveError)}</p>{/if}

  {#if error}
    <AssistFailure {error} />
  {/if}

  <!-- Said here rather than only on the button, because this is the moment
       somebody decides whether to trust it. -->
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
  .source-comparison {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: var(--space-4);
    margin-top: var(--space-4);
    align-items: start;
  }
  .original {
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }
  .original h3,
  .original h4 {
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
  }
  .original img {
    width: 100%;
    height: auto;
    border-radius: var(--radius-md);
  }
  .source-text {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
    font-size: var(--text-sm);
  }
  @media (max-width: 40rem) {
    .source-comparison {
      grid-template-columns: minmax(0, 1fr);
    }
  }

  .lead {
    max-width: var(--measure);
    color: var(--text-muted);
    line-height: var(--leading-normal);
  }

  /* Its own ground rather than a tint: the assistant's glow round the edge is
     what marks it out, and an accent wash underneath would muddy the colours. */
  .progress {
    position: relative;
    isolation: isolate;
    margin: var(--space-4) var(--space-1) 0;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
  }

  .forming {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin-top: var(--space-4);
    padding: var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .parts {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin-top: var(--space-4);
    list-style: none;
  }

  .part {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding-bottom: var(--space-4);
    border-bottom: 1px solid var(--border);
  }

  /* Two columns where there is room, stacked where there is not: reading a
     before against an after side by side is the whole job of this dialog, and
     on a phone the two lines one above the other say the same thing. */
  .compare {
    display: grid;
    gap: var(--space-2);
    padding-left: var(--space-6);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .side {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .which {
    color: var(--text-subtle);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .was {
    color: var(--text-muted);
  }

  .steps {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-4);
    padding-left: var(--space-6);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .stepTitle {
    display: block;
    font-weight: var(--weight-semibold);
  }

  .warning {
    max-width: var(--measure);
    margin-top: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .arrival {
    animation: arrive 420ms var(--ease-out) both;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      filter: blur(3px);
      transform: translateY(0.45rem);
    }

    to {
      opacity: 1;
      filter: blur(0);
      transform: translateY(0);
    }
  }

  @media (min-width: 40rem) {
    .compare {
      grid-template-columns: 1fr 1fr;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .arrival {
      animation: none;
    }
  }
</style>
