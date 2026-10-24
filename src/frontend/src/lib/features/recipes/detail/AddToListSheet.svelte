<script lang="ts">
  import { Button, Checkbox, Sheet } from '$ds';

  import { upcoming } from '$features/planning/upcoming.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { dayOf } from '$features/planning/weekDates';
  import { openList } from '$features/shopping/openList';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  import { createScaling } from '../surface/scaled.svelte';
  import { everyIngredient, type Recipe } from '../types';
  import { wordYield } from '../yieldWords';

  /**
   * A recipe onto the shopping list, one line per ingredient to keep or leave out: what this recipe
   * already put there starts unticked (another recipe's butter is not this one's, so that adds up),
   * and the cook unticks what the cupboard holds. When the recipe is
   * planned and that meal is not shopped yet, it goes on for that meal (its day and servings), so
   * planning and shopping never count it twice.
   */
  interface Props {
    open: boolean;
    householdId: string;
    recipe: Recipe;
    /** The yield on screen, used when no planned meal says otherwise. */
    servings: number;
    onclose: () => void;
  }

  let { open, householdId, recipe, servings, onclose }: Props = $props();

  const planned = $derived(upcoming.nextFor(recipe.id));
  /** The coming meal this shopping is for, when it still needs its shopping. */
  const forMeal = $derived(planned && !planned.isOnShoppingList ? planned : null);
  const amountFor = $derived(forMeal?.servings ?? servings);

  const scaling = createScaling(
    () => recipe,
    () => amountFor
  );

  const ingredients = $derived(everyIngredient(recipe).filter((one) => one.name.trim()));

  /** Whether a line of this name is on the list, and whether this recipe put it there. */
  function listing(name: string): 'here' | 'elsewhere' | null {
    const item = shopping.itemNamed(name);

    if (!item) return null;

    return (item.sources ?? []).some((source) => source.recipeId === recipe.id)
      ? 'here'
      : 'elsewhere';
  }

  const id = $props.id();

  let leftOut = $state<readonly string[]>([]);
  let saving = $state(false);

  $effect(() => {
    if (open) {
      void shopping.load(householdId);
      void upcoming.load(householdId);
    }
  });

  // Asked once per opening, after the list has been read: what it already holds starts unticked.
  let prepared = false;

  $effect(() => {
    if (!open) {
      prepared = false;

      return;
    }

    if (!prepared && shopping.householdId === householdId) {
      prepared = true;
      leftOut = ingredients.filter((one) => listing(one.name) === 'here').map((one) => one.id);
    }
  });

  const kept = $derived(ingredients.length - leftOut.length);

  const when = $derived(
    forMeal
      ? `${new Intl.DateTimeFormat(preferences.locale, { weekday: 'long' }).format(dayOf(forMeal.date))}, ${slotLabel[forMeal.slot]()}`
      : ''
  );

  function toggle(id: string, keep: boolean) {
    leftOut = keep ? leftOut.filter((one) => one !== id) : [...leftOut, id];
  }

  async function confirm() {
    if (saving || kept === 0) return;

    saving = true;

    const failure = forMeal
      ? await shopping.addPlannedMeal(householdId, forMeal.date, forMeal.entryId, leftOut)
      : await shopping.addRecipe(householdId, recipe.id, servings, leftOut);

    saving = false;

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });

      return;
    }

    if (forMeal) {
      // The week and tonight's meal now say it is shopped.
      void upcoming.load(householdId);
    }

    const said = when;

    toaster.show({
      message: () => (said ? m['shopping.addedForMeal']({ when: said }) : m['shopping.added']()),
      tone: 'success',
      action: openList()
    });
    onclose();
  }
</script>

<Sheet {open} title={m['shopping.addToList']()} closeLabel={m['shopping.sheet.cancel']()} {onclose}>
  <div class="body">
    {#if forMeal}
      <p class="hint">
        {m['shopping.sheet.forMeal']({ when, servings: wordYield(forMeal.servings, recipe) })}
      </p>
    {/if}
    <p class="hint">{m['shopping.sheet.hint']()}</p>

    <ul class="lines">
      {#each ingredients as ingredient (ingredient.id)}
        {@const listed = listing(ingredient.name)}
        <li>
          <Checkbox
            checked={!leftOut.includes(ingredient.id)}
            label={`${scaling.amountFor(ingredient).text} ${scaling.nameFor(ingredient)}`.trim()}
            describedBy={listed ? `${id}-${ingredient.id}` : undefined}
            onchange={(keep) => toggle(ingredient.id, keep)}
          />
          {#if listed}
            <span class="listed" id="{id}-{ingredient.id}">
              {listed === 'here' ? m['shopping.sheet.onList']() : m['shopping.sheet.addsUp']()}
            </span>
          {/if}
        </li>
      {/each}
    </ul>
  </div>

  {#snippet footer()}
    <Button onclick={onclose}>{m['shopping.sheet.cancel']()}</Button>
    <Button variant="primary" loading={saving} disabled={kept === 0} onclick={confirm}>
      {m['shopping.sheet.confirm']({ count: kept })}
    </Button>
  {/snippet}
</Sheet>

<style>
  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .hint {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .listed {
    display: block;
    margin-inline-start: var(--space-8);
    color: var(--text-muted);
    font-size: var(--text-xs);
  }

  .lines {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }
</style>
