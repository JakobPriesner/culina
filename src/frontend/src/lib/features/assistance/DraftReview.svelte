<script lang="ts">
  import { Button, Checkbox, GenerationStatus, Modal, Skeleton } from '$ds';
  import { m } from '$shell/i18n';

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
    onaccept: (patch: Partial<Recipe>) => void;
    onclose: () => void;
  }

  let { open = $bindable(), draft, current, writing = false, onaccept, onclose }: Props = $props();

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
  title={m['assist.improve.title']()}
  closeLabel={m['assist.close']()}
  onclose={close}
>
  <p class="lead">{m['assist.improve.lead']()}</p>

  {#if writing}
    <div class="progress">
      <GenerationStatus
        label={draft ? m['assist.improve.writing']() : m['assist.improve.asking']()}
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
  {:else if parts.length === 0 && !writing}
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

  <!-- Said here rather than only on the button, because this is the moment
       somebody decides whether to trust it. -->
  {#if draft}
    <p class="warning">{m['assist.warning']()}</p>
  {/if}

  {#snippet footer()}
    <Button variant="ghost" onclick={close}>{m['assist.discard']()}</Button>

    {#if draft && parts.length > 0}
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

  .progress {
    margin-top: var(--space-3);
    padding: var(--space-3);
    border: 1px solid var(--border-strong);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
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
    animation: arrive 320ms var(--ease-out) both;
  }

  @keyframes arrive {
    from {
      opacity: 0;
      transform: translateY(0.45rem);
    }

    to {
      opacity: 1;
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
