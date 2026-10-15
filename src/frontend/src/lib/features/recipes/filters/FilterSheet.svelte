<script lang="ts">
  import { Button, Field, RadioGroup, Sheet, type RadioOption } from '$ds';
  import { tags } from '$features/cookbooks/stores/tags.svelte';
  import { m } from '$shell/i18n';

  import {
    effectiveSort,
    sortsFor,
    timeCeilings,
    calorieCeilings,
    type RecipeQuery,
    type RecipeSort,
    type SortContext
  } from '../stores/libraryView.svelte';
  import { calorieLabel, sortLabel, timeLabel } from './labels';
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
   * order. Resolved as the list resolves it, so the sheet and the list name the same order.
   */
  const chosen = $derived(effectiveSort(view.sort, context));

  const ceilings = $derived<readonly (number | null)[]>([null, ...timeCeilings]);

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

    <Field label={m['filters.calories']()} hint={m['filters.calories.hint']()} group>
      {#snippet children({ describedBy })}
        <div class="ceilings" aria-describedby={describedBy}>
          {#each [null, ...calorieCeilings] as kcal (kcal ?? 'any')}
            <button
              type="button"
              class="ceiling"
              class:on={view.maxKcal === kcal}
              aria-pressed={view.maxKcal === kcal}
              onclick={() => (view.maxKcal = kcal)}>{calorieLabel(kcal)}</button
            >
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
    <!-- Short words on the buttons, the sheet says what they act on; the full name is what is announced, and contains the word shown. -->
    <div class="actions">
      <Button variant="ghost" size="sm" label={m['filters.clear']()} onclick={() => view.clear()}
        >{m['filters.clearShort']()}</Button
      >
      {#if onsave}
        <Button size="sm" label={m['saved.save']()} onclick={onsave}
          >{m['saved.saveShort']()}</Button
        >
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
   * The secondary actions share what the primary one leaves and wrap their text between words, never inside one.
   */
  .actions {
    display: flex;
    flex: 1;
    align-items: stretch;
    gap: var(--space-1);
  }

  .actions > :global(button) {
    flex: 1 1 0;
    min-width: min-content;
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

  @media (hover: hover) {
    .ceiling:hover {
      border-color: var(--border-strong);
    }
  }

  .ceiling.on {
    border-color: var(--accent);
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-weight: var(--weight-medium);
  }
</style>
