<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button, SearchField, Sheet, Skeleton } from '$ds';
  import { formatNumber, m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';
  import { preferences } from '$shell/preferences.svelte';

  import { roundForLabel } from './rounding';
  import { searchFoods } from './stores/foods';
  import type { FoodHit, NutritionFood, NutritionLine } from './types';
  import UnitWeightForm from './UnitWeightForm.svelte';
  import { gramsWords } from './weights';

  /**
   * What an ingredient name really is, answered in one tap, like the shopping list's "move to section":
   * the food's usual kinds first (the current one ticked), what one unit of it weighs when the recipe counts
   * in units, then a search of the table. It says plainly that the answer holds for every recipe of the
   * household, and nothing in it blocks the page: a search that fails only says so.
   */
  interface Props {
    /** The ingredient as written; null while the sheet is shut. */
    name: string | null;
    /** What the line is now: its food, and whether the household chose it. */
    line: NutritionLine | null;
    /** The recipe's own word for the unit of the line ("clove", "tbsp"); null when it has none to weigh. */
    unit?: string | null;
    /** What one unit weighs on this line now, as far as the line says. */
    perUnit?: number | null;
    onchoose: (food: FoodHit) => void;
    onexclude: () => void;
    onreset: () => void;
    /** Says what one unit weighs, or (null) forgets what the household said. */
    onweigh?: (grams: number | null) => void;
    onclose: () => void;
  }

  let {
    name,
    line,
    unit = null,
    perUnit = null,
    onchoose,
    onexclude,
    onreset,
    onweigh,
    onclose
  }: Props = $props();

  const delayMs = 250;

  let typed = $state('');
  let foods = $state<readonly FoodHit[]>([]);
  let searched = $state<string | null>(null);
  let failed = $state(false);
  let timer: ReturnType<typeof setTimeout> | undefined;
  let latest = 0;

  const loading = createLoadingState();

  onDestroy(() => {
    clearTimeout(timer);
    loading.dispose();
  });

  async function search(query: string) {
    const mine = ++latest;

    loading.start();

    const result = await searchFoods(query.trim());

    if (mine !== latest) {
      return;
    }

    loading.stop();
    failed = !result.ok;
    foods = result.ok ? result.foods : [];
    searched = query.trim();
  }

  // Opens on the name itself: the likeliest answer is a better-named food for exactly what was written.
  $effect(() => {
    clearTimeout(timer);

    if (name === null) {
      latest++;
      loading.stop();
      foods = [];
      searched = null;
      failed = false;

      return;
    }

    typed = name;
    void search(name);
  });

  function typing(value: string) {
    clearTimeout(timer);
    timer = setTimeout(() => void search(value), delayMs);
  }

  const foodName = (food: { nameDe: string; nameEn: string }) =>
    preferences.locale === 'de' ? food.nameDe : food.nameEn;

  const foodLabel = (food: NutritionFood) =>
    preferences.locale === 'de' ? food.labelDe : food.labelEn;

  /** The table's name under the friendlier one; nothing when they are the same words. */
  const bls = (food: NutritionFood) =>
    foodName(food) === foodLabel(food) ? null : m['nutrition.bls']({ name: foodName(food) });

  const excluded = $derived(line?.status === 'excluded');
  const current = $derived(line?.food ?? null);
  const corrected = $derived(line?.corrected ?? false);
  const variants = $derived(line?.variants ?? []);
  const variantIsCurrent = $derived(variants.some((one) => one.code === current?.code));

  const variantLabel = (variant: { labelDe: string; labelEn: string }) =>
    preferences.locale === 'de' ? variant.labelDe : variant.labelEn;

  const chooseVariant = (variant: (typeof variants)[number]) =>
    onchoose({
      code: variant.code,
      nameDe: variant.labelDe,
      nameEn: variant.labelEn,
      energyKcal: variant.energyKcal
    });

  /** The weight section: only for a line counted in units, with a way to say it. */
  const weighing = $derived(Boolean(onweigh && unit && line?.unitKey));
  const householdWeight = $derived(line?.via === 'householdWeight');
  const typicalWeight = $derived(line?.via === 'typicalWeight');
  const typicalHint = $derived(
    typicalWeight && perUnit !== null
      ? [m['nutrition.weight.typical']({ grams: gramsWords(perUnit) }), line?.source]
          .filter(Boolean)
          .join(' · ')
      : undefined
  );

  /** The food on show above is not offered a second time. */
  const others = $derived(foods.filter((food) => food.code !== current?.code));

  const per100 = (kcal: number) =>
    m['nutrition.correct.per100']({
      kcal: formatNumber(roundForLabel('energy', kcal, false).value)
    });
</script>

{#snippet mark(here: boolean)}
  <span class="mark" aria-hidden="true">
    {#if here}
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="m5 12 5 5 9-10" />
      </svg>
    {/if}
  </span>
{/snippet}

<Sheet
  open={name !== null}
  title={m['nutrition.correct.title']({ name: name ?? '' })}
  closeLabel={m['nutrition.correct.close']()}
  {onclose}
>
  <div class="body">
    <p class="reach">{m['nutrition.correct.reach']({ name: name ?? '' })}</p>

    {#if variants.length > 0}
      <h3 class="sectionTitle">{m['nutrition.variants.title']()}</h3>
      <ul class="foods variantsList" aria-label={m['nutrition.variants.title']()}>
        {#each variants as variant (variant.code)}
          {@const here = variant.code === current?.code}
          <li>
            <button
              type="button"
              class="food"
              aria-current={here ? 'true' : undefined}
              onclick={() => (here ? onclose() : chooseVariant(variant))}
            >
              {@render mark(here)}
              <span class="words">
                <span class="name">{variantLabel(variant)}</span>
                {#if variant.energyKcal !== null}
                  <span class="quiet">{per100(variant.energyKcal)}</span>
                {/if}
              </span>
            </button>
          </li>
        {/each}
      </ul>
    {/if}

    {#if (current || excluded) && !variantIsCurrent}
      <h3 class="sectionTitle">{m['nutrition.correct.now']()}</h3>
      <ul class="foods" aria-label={m['nutrition.correct.now']()}>
        <li>
          <button type="button" class="food" aria-current="true" onclick={onclose}>
            {@render mark(true)}
            <span class="words">
              {#if current}
                <span class="name">{foodLabel(current)}</span>
                {#if bls(current)}<span class="quiet">{bls(current)}</span>{/if}
                <span class="quiet">
                  {corrected ? m['nutrition.correct.yours']() : m['nutrition.correct.default']()}
                </span>
              {:else}
                <span class="name">{m['nutrition.correct.exclude']()}</span>
                <span class="quiet">{m['nutrition.reason.excluded']()}</span>
              {/if}
            </span>
          </button>
        </li>
      </ul>
    {/if}

    {#if weighing && unit && name !== null}
      <section class="weight" aria-labelledby="nutrition-weight-title">
        <h3 id="nutrition-weight-title" class="sectionTitle">
          {m['nutrition.weight.title']({ unit, name })}
        </h3>
        <p class="quiet">{m['nutrition.weight.reach']({ unit, name })}</p>

        {#key `${name}:${line?.unitKey}`}
          <UnitWeightForm
            label={m['nutrition.weight.field']()}
            initial={householdWeight || typicalWeight ? perUnit : null}
            hint={typicalHint}
            saveLabel={m['nutrition.weight.save']()}
            removeLabel={householdWeight ? m['nutrition.weight.remove']() : undefined}
            onsave={(grams) => onweigh?.(grams)}
            onremove={() => onweigh?.(null)}
          />
        {/key}
      </section>
    {/if}

    <SearchField
      id="nutrition-food-search"
      bind:value={typed}
      label={variants.length > 0
        ? m['nutrition.correct.searchOther']()
        : m['nutrition.correct.search']()}
      placeholder={variants.length > 0
        ? m['nutrition.correct.searchOther']()
        : m['nutrition.correct.search']()}
      clearLabel={m['nutrition.correct.clear']()}
      oninput={typing}
    />

    <div class="results" aria-live="polite" aria-busy={loading.showing}>
      {#if loading.showing}
        <div class="skeletons" aria-label={m['nutrition.correct.loading']()}>
          {#each [0, 1, 2, 3] as row (row)}
            <Skeleton width={row % 2 ? '70%' : '85%'} height="1.25rem" />
          {/each}
        </div>
      {:else if failed}
        <p class="quiet">{m['nutrition.correct.failed']()}</p>
      {:else if searched !== null && others.length === 0}
        <p class="quiet">{m['nutrition.correct.none']({ query: searched })}</p>
      {:else}
        <ul class="foods">
          {#each others as food (food.code)}
            <li>
              <button type="button" class="food" onclick={() => onchoose(food)}>
                {@render mark(false)}
                <span class="words">
                  <span class="name">{foodName(food)}</span>
                  {#if food.energyKcal !== null}
                    <span class="quiet">{per100(food.energyKcal)}</span>
                  {/if}
                </span>
              </button>
            </li>
          {/each}
        </ul>
      {/if}
    </div>
  </div>

  {#snippet footer()}
    {#if corrected}
      <Button onclick={onreset}>{m['nutrition.correct.reset']()}</Button>
    {/if}
    {#if !excluded}
      <Button onclick={onexclude}>{m['nutrition.correct.exclude']()}</Button>
    {/if}
  {/snippet}
</Sheet>

<style>
  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    min-width: 0;
  }

  .reach,
  .quiet {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .reach {
    padding: var(--space-3) var(--space-4);
    background: var(--surface-sunken);
    border-radius: var(--radius-md);
  }

  .sectionTitle {
    margin-bottom: calc(var(--space-2) * -1);
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
  }

  .foods {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .food {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
    min-height: var(--control-md);
    padding: var(--space-3);
    border: 1px solid transparent;
    border-radius: var(--radius-md);
    background: none;
    color: var(--text);
    font: inherit;
    text-align: start;
    cursor: pointer;
  }

  .food:hover {
    background: var(--surface-hover);
  }

  .food[aria-current='true'] {
    border-color: var(--border-accent);
    background: var(--surface-accent-subtle);
  }

  .weight {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .results .foods > li + li,
  .variantsList > li + li {
    border-top: 1px solid var(--border);
  }

  .words {
    display: flex;
    flex-direction: column;
    min-width: 0;
    gap: var(--space-1);
  }

  .name {
    overflow-wrap: anywhere;
    font-weight: var(--weight-medium);
  }

  .mark {
    display: grid;
    flex: none;
    place-items: center;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--accent);
  }

  .mark svg {
    width: 100%;
    height: 100%;
  }

  .skeletons {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding: var(--space-2) var(--space-3);
  }

  .results {
    min-height: 12rem;
  }
</style>
