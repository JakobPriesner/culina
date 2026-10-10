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
   * Everything the library can be asked, in one `Sheet` for every screen size.
   * Writes straight into the query object, not a draft applied on close, so the effect of a filter
   * is visible while setting it.
   */
  interface Props {
    open: boolean;
    householdId: string;
    view: RecipeQuery;
    context: SortContext;
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
   * The resolved order, not the stored null: radios with none chosen would say the list has no
   * order.
   */
  const chosen = $derived(view.sort ?? defaultOf(orders));

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
    <div class="actions">
      <Button variant="ghost" size="sm" onclick={() => view.clear()}>{m['filters.clear']()}</Button>
      {#if onsave}
        <Button size="sm" onclick={onsave}>{m['saved.save']()}</Button>
      {/if}
      <Button variant="primary" onclick={onclose}>{m['filters.close']()}</Button>
    </div>
  {/snippet}
</Sheet>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  /*
   * One row at every width: stacking three buttons would leave the choices a sliver of a short screen.
   * The secondary actions share what the primary one leaves and wrap their text if they must.
   */
  .actions {
    display: flex;
    flex: 1;
    align-items: stretch;
    gap: var(--space-1);
  }

  .actions > :global(button) {
    flex: 1 1 0;
    padding-inline: var(--space-2);
  }

  .actions > :global(button:last-child) {
    flex: 0 0 auto;
    padding-inline: var(--space-3);
  }

  .ceilings {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* Not FilterChip: these are one-of-several, not independent toggles. */
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
