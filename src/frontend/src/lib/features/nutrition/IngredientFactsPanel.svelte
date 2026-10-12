<script lang="ts">
  import type { AppError } from '$api';
  import { Button, Skeleton, Switch } from '$ds';
  import { unitLabel } from '$features/recipes/quantityLabels';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';
  import { toaster } from '$shell/toaster.svelte';

  import NutritionCorrectionSheet from './NutritionCorrectionSheet.svelte';
  import { ingredientFacts as store } from './stores/ingredientFacts.svelte';
  import type { FoodHit, IngredientFact, NutritionLine } from './types';
  import UnitWeightForm from './UnitWeightForm.svelte';
  import { gramsWords } from './weights';

  /**
   * What a household said about its ingredients, in one place: whether typical weights count, then each name with
   * its food and the weights of its units, every one to change or take back. It is the household's alone; a member
   * sees it, anyone else would be told there is no such household by the server.
   */
  interface Props {
    householdId: string;
  }

  let { householdId }: Props = $props();

  const status = $derived(store.status(householdId));
  const facts = $derived(store.facts(householdId));

  /** The name whose food is being changed in the sheet, and the weight (name and unit) whose number is being typed. */
  let choosing = $state<string | null>(null);
  let weighing = $state<{ name: string; unit: string } | null>(null);

  $effect(() => {
    void store.load(householdId);
  });

  const chosen = $derived(facts.find((fact) => fact.name === choosing) ?? null);

  /** The sheet reads a line; the fact is what the household chose for the name. */
  const lineOf = (fact: IngredientFact): NutritionLine => ({
    ingredientId: fact.name,
    status: fact.food ? 'counted' : 'excluded',
    reason: null,
    food: fact.food,
    grams: null,
    via: null,
    corrected: true,
    canRaiseEnergy: false,
    energyKcal: null
  });

  const foodIn = (fact: IngredientFact) =>
    fact.food ? (preferences.locale === 'de' ? fact.food.labelDe : fact.food.labelEn) : null;

  /** Tells the person how it went; a refusal says why and the list is already back to what it was. */
  function said(failure: AppError | null, success: () => string) {
    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: success, tone: 'success' }
    );
  }

  async function chooseFood(name: string, hit: FoodHit | null) {
    choosing = null;
    said(await store.setFood(householdId, name, hit?.code ?? null), () =>
      hit
        ? m['nutrition.correct.done']({
            name,
            food: preferences.locale === 'de' ? hit.nameDe : hit.nameEn
          })
        : m['nutrition.correct.doneExcluded']({ name })
    );
  }

  async function forgetFood(name: string) {
    choosing = null;
    said(await store.removeFood(householdId, name), () =>
      m['nutrition.correct.doneDefault']({ name })
    );
  }

  async function saveWeight(name: string, unit: string, grams: number) {
    weighing = null;
    said(await store.setWeight(householdId, name, unit, grams), () =>
      m['nutrition.weight.done']({ name, unit: unitLabel(unit), grams: gramsWords(grams) })
    );
  }

  async function forgetWeight(name: string, unit: string) {
    weighing = null;
    said(await store.removeWeight(householdId, name, unit), () =>
      m['nutrition.weight.doneRemoved']({ name, unit: unitLabel(unit) })
    );
  }

  async function allowTypical(on: boolean) {
    const failure = await store.setUseTypicalWeights(householdId, on);

    if (failure) {
      toaster.show({ message: () => explain(failure), tone: 'danger' });
    }
  }
</script>

<div class="panel">
  <Switch
    checked={store.useTypicalWeights(householdId)}
    label={m['household.ingredients.typical']()}
    description={m['household.ingredients.typicalHint']()}
    disabled={status !== 'ready'}
    onchange={(on) => void allowTypical(on)}
  />

  {#if status === 'failed'}
    <div class="failed" role="status">
      <p>{m['household.ingredients.failed']()}</p>
      <Button size="sm" onclick={() => void store.load(householdId)}>{m['error.retry']()}</Button>
    </div>
  {:else if status !== 'ready'}
    <div
      class="skeletons"
      role="status"
      aria-busy="true"
      aria-label={m['household.ingredients.loading']()}
    >
      {#each [0, 1, 2] as row (row)}
        <Skeleton width={row % 2 ? '60%' : '80%'} height="1.25rem" />
      {/each}
    </div>
  {:else if facts.length === 0}
    <p class="quiet">{m['household.ingredients.empty']()}</p>
  {:else}
    <ul class="facts" aria-label={m['household.ingredients.title']()}>
      {#each facts as fact (fact.name)}
        <li class="fact">
          <h3 class="name">{fact.name}</h3>

          <ul class="lines">
            {#if fact.corrected}
              {@const what = foodIn(fact)
                ? m['household.ingredients.food']({ food: foodIn(fact) ?? '' })
                : m['household.ingredients.excluded']()}
              <li class="line">
                <span class="words">{what}</span>
                <span class="actions">
                  <Button
                    size="sm"
                    label={m['household.ingredients.editLabel']({ name: fact.name, what })}
                    onclick={() => (choosing = fact.name)}
                  >
                    {m['household.ingredients.edit']()}
                  </Button>
                  <Button
                    size="sm"
                    label={m['household.ingredients.removeLabel']({ name: fact.name, what })}
                    onclick={() => void forgetFood(fact.name)}
                  >
                    {m['household.ingredients.remove']()}
                  </Button>
                </span>
              </li>
            {/if}

            {#each fact.weights as weight (weight.unit)}
              {@const unit = unitLabel(weight.unit)}
              {@const what = m['household.ingredients.weight']({
                unit,
                grams: gramsWords(weight.grams)
              })}
              <li class="line">
                <span class="words">{what}</span>
                <span class="actions">
                  <Button
                    size="sm"
                    label={m['household.ingredients.editLabel']({ name: fact.name, what })}
                    onclick={() => (weighing = { name: fact.name, unit: weight.unit })}
                  >
                    {m['household.ingredients.edit']()}
                  </Button>
                  <Button
                    size="sm"
                    label={m['household.ingredients.removeLabel']({ name: fact.name, what })}
                    onclick={() => void forgetWeight(fact.name, weight.unit)}
                  >
                    {m['household.ingredients.remove']()}
                  </Button>
                </span>

                {#if weighing?.name === fact.name && weighing.unit === weight.unit}
                  <div class="form">
                    <UnitWeightForm
                      label={m['nutrition.weight.title']({ unit, name: fact.name })}
                      initial={weight.grams}
                      saveLabel={m['nutrition.weight.save']()}
                      onsave={(grams) => void saveWeight(fact.name, weight.unit, grams)}
                    />
                  </div>
                {/if}
              </li>
            {/each}
          </ul>
        </li>
      {/each}
    </ul>
  {/if}
</div>

<NutritionCorrectionSheet
  name={chosen?.name ?? null}
  {householdId}
  line={chosen ? lineOf(chosen) : null}
  onchoose={(hit) => chosen && void chooseFood(chosen.name, hit)}
  onexclude={() => chosen && void chooseFood(chosen.name, null)}
  onreset={() => chosen && void forgetFood(chosen.name)}
  onclose={() => (choosing = null)}
/>

<style>
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: var(--measure);
    min-width: 0;
  }

  .quiet,
  .failed {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .failed {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-4);
  }

  .skeletons {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .facts,
  .lines {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .fact {
    padding-block: var(--space-3);
    border-bottom: 1px solid var(--border);
  }

  .name {
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    overflow-wrap: anywhere;
  }

  .line {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2) var(--space-4);
    min-height: var(--control-md);
  }

  .words {
    min-width: 0;
    overflow-wrap: anywhere;
  }

  .actions {
    display: flex;
    gap: var(--space-2);
  }

  .form {
    flex-basis: 100%;
    padding-bottom: var(--space-2);
  }
</style>
