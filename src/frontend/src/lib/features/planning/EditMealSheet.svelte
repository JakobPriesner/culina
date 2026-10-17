<script lang="ts">
  import { Button, Checkbox, Sheet } from '$ds';

  import { m } from '$shell/i18n';

  import MealFields from './MealFields.svelte';
  import { servingsOf, type MealSlot, type PlannedDay, type PlannedMeal } from './mealPlan.svelte';

  /**
   * One planned meal, answered rather than aimed at: where it goes, and for how many. The keyboard's and screen reader's only way to move it, and the only place
   * the slot can change (a drag that silently turned dinner into breakfast would be unaimable). Servings are this meal's own; the recipe keeps its.
   * Laid out like planning a recipe (`MealFields`), so changing a meal is the same form as choosing it.
   */
  interface Props {
    meal: PlannedMeal | null;
    days: readonly PlannedDay[];
    /** Which day it is on now, so the sheet opens on the answer it already has. */
    from: string;
    onsave: (to: { date: string; slot: MealSlot; servings: number; updateList: boolean }) => void;
    onclose: () => void;
  }

  let { meal, days, from, onsave, onclose }: Props = $props();

  const id = $props.id();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let servings = $state(1);
  let updateList = $state(true);

  /** The figure the stepper shows is whole; a meal planned for 7.4 keeps its 7.4 until somebody says otherwise. */
  let touched = $state(false);

  // Set here so opening the sheet for another meal starts from that meal's day.
  $effect(() => {
    if (meal) {
      date = from;
      slot = (meal.slot as MealSlot) ?? 'dinner';
      servings = Math.max(1, Math.round(servingsOf(meal)));
      updateList = true;
      touched = false;
    }
  });

  const answer = () =>
    onsave({
      date,
      slot,
      servings: meal && !touched ? servingsOf(meal) : servings,
      updateList
    });
</script>

<Sheet
  open={meal !== null}
  title={meal ? m['plan.edit.title']({ title: meal.title }) : ''}
  closeLabel={m['plan.move.close']()}
  {onclose}
>
  {#if meal}
    <MealFields
      {days}
      bind:date
      bind:slot
      bind:servings
      onservings={() => (touched = true)}
      servingsHint={m['plan.edit.servingsHint']({ count: meal.recipeServings })}
    >
      {#if meal.isOnShoppingList}
        <!-- The list holds a sum, so what it already has of this meal is said, and what following would and would not touch. -->
        <div class="list">
          <p class="hint">{m['plan.edit.onList']({ count: servingsOf(meal) })}</p>
          <Checkbox
            bind:checked={updateList}
            label={m['plan.edit.updateList']()}
            describedBy="{id}-list-hint"
          />
          <p class="hint" id="{id}-list-hint">{m['plan.edit.updateListHint']()}</p>
        </div>
      {/if}
    </MealFields>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.move.cancel']()}</Button>
    <Button variant="primary" onclick={answer}>
      {m['plan.edit.confirm']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .list {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
  }

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
