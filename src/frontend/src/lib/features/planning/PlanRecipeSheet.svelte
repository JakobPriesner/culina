<script lang="ts">
  import { Button, RadioGroup, Sheet, Skeleton, type RadioOption } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  import { asDate, mealPlan, type MealSlot } from './mealPlan.svelte';

  /**
   * The short path from deciding on a recipe to putting it in the week.
   *
   * This uses the same week, day descriptions and meal slots as the plan page,
   * rather than growing a second, smaller planning model inside recipe detail.
   * The servings are supplied by the detail page, where scaling already lives,
   * so the amount somebody chose is the amount the plan remembers.
   */
  interface Props {
    open: boolean;
    householdId: string;
    recipeId: string;
    title: string;
    servings: number;
    onclose: () => void;
  }

  let { open, householdId, recipeId, title, servings, onclose }: Props = $props();

  let date = $state('');
  let slot = $state<MealSlot>('dinner');
  let saving = $state(false);

  $effect(() => {
    if (!open) {
      return;
    }

    date = '';
    slot = 'dinner';
    void mealPlan.load(householdId);
  });

  // Prefer today when it is in this week, then the first day. Once the person
  // chooses, keep that answer while the store changes around it.
  $effect(() => {
    if (!open || mealPlan.days.length === 0 || mealPlan.days.some((day) => day.date === date)) {
      return;
    }

    const today = asDate(new Date());
    date = mealPlan.days.some((day) => day.date === today) ? today : (mealPlan.days[0]?.date ?? '');
  });

  const weekdays = $derived(
    new Intl.DateTimeFormat(preferences.locale, {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    })
  );

  /** Midday keeps a bare date on the date it names in every time zone. */
  const dayOf = (day: string) => new Date(`${day}T12:00:00`);

  const dayOptions = $derived<readonly RadioOption[]>(
    mealPlan.days.map((day) => ({
      value: day.date,
      label: weekdays.format(dayOf(day.date)),
      description:
        day.meals.length > 0
          ? day.meals.map((meal) => meal.title).join(', ')
          : m['plan.move.freeDay']()
    }))
  );

  const slotOptions = $derived<readonly RadioOption[]>(
    (['breakfast', 'lunch', 'dinner'] as const).map((which) => ({
      value: which,
      label: m[`plan.slot.${which}`]()
    }))
  );

  async function confirm() {
    if (!date || saving) {
      return;
    }

    saving = true;
    const ok = await mealPlan.plan(householdId, { date, recipeId, servings, slot });
    saving = false;

    if (!ok) {
      toaster.show({
        message: () => (mealPlan.error ? explain(mealPlan.error) : m['plan.recipe.failed']()),
        tone: 'danger'
      });

      return;
    }

    toaster.show({
      message: () => m['plan.recipe.done']({ day: weekdays.format(dayOf(date)) }),
      tone: 'success'
    });
    onclose();
  }
</script>

<Sheet
  {open}
  title={m['plan.recipe.title']({ title })}
  closeLabel={m['plan.recipe.close']()}
  {onclose}
>
  {#if mealPlan.loading && mealPlan.days.length === 0}
    <div class="loading" aria-label={m['plan.recipe.loading']()} aria-busy="true">
      <Skeleton width="70%" height="1.5rem" />
      <Skeleton width="85%" height="1.5rem" />
      <Skeleton width="60%" height="1.5rem" />
    </div>
  {:else if mealPlan.error && mealPlan.days.length === 0}
    <p class="failure">{explain(mealPlan.error)}</p>
  {:else}
    <div class="answers">
      <fieldset>
        <legend>{m['plan.recipe.day']()}</legend>
        <RadioGroup name="plan-recipe-day" bind:value={date} options={dayOptions} />
      </fieldset>

      <fieldset>
        <legend>{m['plan.pick.slot']()}</legend>
        <RadioGroup name="plan-recipe-slot" bind:value={slot} options={slotOptions} />
      </fieldset>
    </div>
  {/if}

  {#snippet footer()}
    <Button onclick={onclose}>{m['plan.recipe.cancel']()}</Button>
    <Button
      variant="primary"
      disabled={!date || mealPlan.loading}
      loading={saving}
      onclick={confirm}
    >
      {m['plan.recipe.confirm']()}
    </Button>
  {/snippet}
</Sheet>

<style>
  .answers,
  .loading {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  fieldset {
    min-width: 0;
    padding: 0;
    border: 0;
  }

  legend {
    margin-bottom: var(--space-2);
    color: var(--text-subtle);
    font-size: var(--text-xs);
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .failure {
    color: var(--text-danger);
  }
</style>
