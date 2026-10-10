<script lang="ts">
  import { onDestroy } from 'svelte';

  import { Button, SearchField, Sheet, Skeleton } from '$ds';
  import { formatNumber, m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';
  import { preferences } from '$shell/preferences.svelte';

  import { roundForLabel } from './rounding';
  import { searchFoods } from './stores/foods';
  import type { FoodHit, NutritionFood, NutritionLine } from './types';

  /**
   * What an ingredient name really is, answered in one tap, like the shopping list's "move to section":
   * the current choice first with a checkmark, then a search of the table. It says plainly that the answer
   * holds for every recipe of the household, and nothing in it blocks the page: a search that fails only says so.
   */
  interface Props {
    /** The ingredient as written; null while the sheet is shut. */
    name: string | null;
    /** What the line is now: its food, and whether the household chose it. */
    line: NutritionLine | null;
    onchoose: (food: FoodHit) => void;
    onexclude: () => void;
    onreset: () => void;
    onclose: () => void;
  }

  let { name, line, onchoose, onexclude, onreset, onclose }: Props = $props();

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

    {#if current || excluded}
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

    <SearchField
      id="nutrition-food-search"
      bind:value={typed}
      label={m['nutrition.correct.search']()}
      placeholder={m['nutrition.correct.search']()}
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

  .results .foods > li + li {
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
