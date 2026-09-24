<script lang="ts">
  import { Button, Field, RadioGroup, Sheet, type RadioOption } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import {
    sortsFor,
    timeCeilings,
    type RecipeQuery,
    type RecipeSort,
    type SortContext
  } from '../stores/libraryView.svelte';
  import { sortLabel, timeLabel } from './labels';
  import TagChooser from './TagChooser.svelte';

  /**
   * Everything the library can be asked, in one panel.
   *
   * A sheet rather than a popover, and the same sheet on every screen size:
   * `Sheet` already rises from the thumb on a phone and becomes a centred
   * dialog on a desktop, so one component here is one behaviour to get right
   * instead of two that drift.
   *
   * The panel writes straight into the query object rather than holding a draft
   * and applying it on close. A filter that only takes effect once you dismiss
   * the thing you set it in is a filter you cannot judge — the point of picking
   * "up to 30 minutes" is seeing how much is left.
   */
  interface Props {
    open: boolean;
    householdId: string;
    view: RecipeQuery;
    context: SortContext;
    /** Offered only where a search can be saved — not inside a cookbook. */
    onsave?: () => void;
    onclose: () => void;
  }

  let { open = $bindable(), householdId, view, context, onsave, onclose }: Props = $props();

  $effect(() => {
    if (open && householdId) {
      void tags.load(householdId);
    }
  });

  const orders = $derived(sortsFor(context));

  const options = $derived<readonly RadioOption[]>(
    orders.map((sort) => ({ value: sort, label: sortLabel(sort) }))
  );

  /**
   * Which order is ticked.
   *
   * The resolved one, not the stored null: a group of radios with none chosen
   * says the list is in no order at all, which is never true.
   */
  const chosen = $derived(view.sort ?? defaultOf(orders));

  /** The ceilings, plus the one that means no ceiling. */
  const ceilings = $derived<readonly (number | null)[]>([null, ...timeCeilings]);

  function defaultOf(available: readonly RecipeSort[]): RecipeSort {
    return available[0] ?? 'recent';
  }

  function chooseSort(value: string) {
    view.sort = value as RecipeSort;
  }
</script>

<Sheet bind:open title={m['filters.title']()} closeLabel={m['filters.close']()} {onclose}>
  <div class="panel">
    <Field label={m['filters.sort']()} hint={m['filters.sort.hint']()} group>
      {#snippet children({ describedBy })}
        <RadioGroup
          name="recipe-sort"
          value={chosen}
          {options}
          {describedBy}
          onchange={chooseSort}
        />
      {/snippet}
    </Field>

    <Field label={m['filters.time']()} hint={m['filters.time.hint']()} group>
      {#snippet children({ describedBy })}
        <div class="ceilings" aria-describedby={describedBy}>
          {#each ceilings as minutes (minutes ?? 'any')}
            <button
              type="button"
              class="ceiling"
              class:on={view.maxMinutes === minutes}
              aria-pressed={view.maxMinutes === minutes}
              onclick={() => (view.maxMinutes = minutes)}
            >
              {timeLabel(minutes)}
            </button>
          {/each}
        </div>
      {/snippet}
    </Field>

    <Field label={m['filters.tags']()} hint={m['filters.tags.hint']()} group>
      <TagChooser
        tags={tags.items}
        selected={view.tags}
        empty={m['filters.tags.none']()}
        ontoggle={(slug) => view.toggleTag(slug)}
      />
    </Field>
  </div>

  {#snippet footer()}
    <Button variant="ghost" onclick={() => view.clear()}>{m['filters.clear']()}</Button>
    {#if onsave}
      <Button onclick={onsave}>{m['saved.save']()}</Button>
    {/if}
    <Button variant="primary" onclick={onclose}>{m['filters.close']()}</Button>
  {/snippet}
</Sheet>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  .ceilings {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* Not FilterChip: these are one-of-several rather than several independent
     toggles, and FilterChip's tick would say "on" about five things that are
     really one answer. */
  .ceiling {
    min-height: var(--control-sm);
    padding: 0 var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface);
    color: var(--text);
    font: inherit;
    font-size: var(--text-sm);
    cursor: pointer;
  }

  .ceiling:hover {
    border-color: var(--border-strong);
  }

  .ceiling.on {
    border-color: var(--accent);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-weight: var(--weight-medium);
  }
</style>
