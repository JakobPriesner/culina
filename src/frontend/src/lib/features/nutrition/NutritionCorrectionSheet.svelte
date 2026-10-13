<script lang="ts">
  import { onDestroy, untrack } from 'svelte';

  import { Button, SearchField, Sheet, Skeleton } from '$ds';
  import { session } from '$features/auth/session.svelte';
  import { formatNumber, m } from '$shell/i18n';
  import { createLoadingState } from '$shell/loadingState.svelte';
  import { preferences } from '$shell/preferences.svelte';

  import PackageFoodForm from './PackageFoodForm.svelte';
  import { roundForLabel } from './rounding';
  import { searchFoods } from './stores/foods';
  import { householdFoods, hitOf, type Label } from './stores/householdFoods.svelte';
  import type { FoodHit, NutritionFood, NutritionLine } from './types';
  import UnitWeightForm from './UnitWeightForm.svelte';
  import { gramsWords } from './weights';

  /**
   * What an ingredient name really is, answered in one tap, like the shopping list's "move to section":
   * what it counts as now and whose choice that is (the table's, the household's, or one it inherits from),
   * then similar foods, what one unit weighs when the recipe counts in units, and a search of the table.
   * The search opens on the name only when there are no similar foods to offer, so a long tail of soups
   * never pushes them away. Nothing in it blocks the page: a search that fails only says so.
   */
  interface Props {
    /** The ingredient as written; null while the sheet is shut. */
    name: string | null;
    /** The household the answer is given for; a choice another household made is named as theirs. */
    householdId?: string | null;
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
    name: opening,
    householdId = null,
    line: lineNow,
    unit = null,
    perUnit = null,
    onchoose,
    onexclude,
    onreset,
    onweigh,
    onclose
  }: Props = $props();

  const delayMs = 250;

  // The caller empties name and line the moment an answer is given, while the sheet is still fading out; what it
  // showed is kept until it is opened again, so it does not read "Was ist „“?" and shrink on the way out.
  let held = $state<{ name: string; line: NutritionLine | null } | null>(null);

  $effect.pre(() => {
    if (opening !== null) {
      held = { name: opening, line: lineNow };
    }
  });

  const name = $derived(opening ?? held?.name ?? null);
  const line = $derived(opening === null ? (held?.line ?? null) : lineNow);

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

    if (query.trim() === '') {
      loading.stop();
      foods = [];
      searched = null;
      failed = false;

      return;
    }

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

  // Without similar foods, the likeliest answer is a better-named food for exactly what was written, so
  // the search opens on the name; with them, it waits for the person to type. Only a new name re-runs it.
  $effect(() => {
    clearTimeout(timer);

    // Closing leaves the last search on show while the sheet fades; the next opening starts afresh.
    if (opening === null) {
      return;
    }

    const offered = untrack(() => (line?.variants ?? []).length > 0);

    const query = offered ? '' : opening;

    typed = query;
    void search(query);
  });

  function typing(value: string) {
    clearTimeout(timer);
    timer = setTimeout(() => void search(value), delayMs);
  }

  const foodName = (food: { nameDe: string; nameEn: string }) =>
    preferences.locale === 'de' ? food.nameDe : food.nameEn;

  const foodLabel = (food: NutritionFood) =>
    preferences.locale === 'de' ? food.labelDe : food.labelEn;

  /** The table's name under the friendlier one, or that a household typed it from a package; nothing when the same words. */
  const bls = (food: NutritionFood) =>
    food.source === 'package'
      ? m['nutrition.fromPackage']()
      : foodName(food) === foodLabel(food)
        ? null
        : m['nutrition.bls']({ name: foodName(food) });

  const per100 = (kcal: number) =>
    m['nutrition.correct.per100']({
      kcal: formatNumber(roundForLabel('energy', kcal, false).value)
    });

  const excluded = $derived(line?.status === 'excluded');
  const current = $derived(line?.food ?? null);
  const corrected = $derived(line?.corrected ?? false);
  const variants = $derived(line?.variants ?? []);
  const currentVariant = $derived(variants.find((one) => one.code === current?.code));
  /** The similar foods other than the one on show above. */
  const alternatives = $derived(variants.filter((one) => one.code !== current?.code));

  /** Whether a household chose for itself; a line from before the server named who counts as its own. */
  const ownChoice = (by: string | null | undefined) => !by || by === householdId;

  /** Another household chose, so taking back is not this household's to do. */
  const inheritedChoice = $derived(corrected && !ownChoice(line?.chosenBy));

  /** Who said so: the table, this household, or a household it inherits from (by name when the session knows it). */
  const whoseWords = (by: string | null | undefined) => {
    if (ownChoice(by)) {
      return m['nutrition.correct.yours']();
    }

    const household = session.householdName(by!);

    return household
      ? m['nutrition.correct.chosenIn']({ household })
      : m['nutrition.correct.chosenAbove']();
  };

  const whose = $derived(corrected ? whoseWords(line?.chosenBy) : m['nutrition.correct.default']());

  /** Its energy, when the similar foods know it, and whose choice it is. */
  const currentDetail = $derived(
    [
      currentVariant && currentVariant.energyKcal !== null
        ? per100(currentVariant.energyKcal)
        : null,
      whose
    ]
      .filter(Boolean)
      .join(' · ')
  );

  /** Taking back an heir's own choice leads to what it inherits, which need not be the default. */
  const inherits = $derived(
    householdId !== null &&
      (session.households.find((one) => one.householdId === householdId)?.inheritsFrom ?? [])
        .length > 0
  );

  const variantLabel = (variant: { labelDe: string; labelEn: string }) =>
    preferences.locale === 'de' ? variant.labelDe : variant.labelEn;

  const chooseVariant = (variant: (typeof variants)[number]) =>
    onchoose({
      code: variant.code,
      nameDe: variant.labelDe,
      nameEn: variant.labelEn,
      energyKcal: variant.energyKcal,
      source: 'bls'
    });

  /** The weight section: only for a line counted in units, with a way to say it. */
  const weighing = $derived(Boolean(onweigh && unit && line?.unitKey));
  const householdWeight = $derived(line?.via === 'householdWeight');
  const ownWeight = $derived(householdWeight && ownChoice(line?.weighedBy));
  const typicalWeight = $derived(line?.via === 'typicalWeight');
  const weightHint = $derived(
    typicalWeight && perUnit !== null
      ? [m['nutrition.weight.typical']({ grams: gramsWords(perUnit) }), line?.source]
          .filter(Boolean)
          .join(' · ')
      : householdWeight && !ownWeight
        ? whoseWords(line?.weighedBy)
        : undefined
  );

  /** The food on show above is not offered a second time. */
  const others = $derived(foods.filter((food) => food.code !== current?.code));

  $effect(() => {
    if (householdId) {
      void householdFoods.load(householdId);
    }
  });

  /** The household's foods from a package that have every word searched for: "kokos light" finds "Kokosmilch light". */
  const packaged = $derived.by(() => {
    const words = (searched ?? '').toLowerCase().split(/\s+/).filter(Boolean);

    return words.length === 0
      ? []
      : householdFoods.foods.filter(
          (food) =>
            food.code !== current?.code &&
            words.every((word) => food.name.toLowerCase().includes(word))
        );
  });

  let adding = $state(false);

  // A sheet opened on another name starts without the form.
  $effect(() => {
    if (opening !== null) {
      adding = false;
    }
  });

  async function addFood(label: Label) {
    const added = await householdFoods.add(householdId!, label);

    if (!added.ok) {
      return added.error;
    }

    adding = false;
    onchoose(hitOf(added.value));

    return null;
  }
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
  open={opening !== null}
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
                <span class="quiet">{currentDetail}</span>
              {:else}
                <span class="name">{m['nutrition.correct.exclude']()}</span>
                <span class="quiet">
                  {inheritedChoice ? whose : m['nutrition.reason.excluded']()}
                </span>
              {/if}
            </span>
          </button>
        </li>
      </ul>
    {/if}

    {#if alternatives.length > 0}
      <h3 class="sectionTitle">{m['nutrition.variants.title']()}</h3>
      <ul class="foods variantsList" aria-label={m['nutrition.variants.title']()}>
        {#each alternatives as variant (variant.code)}
          <li>
            <button type="button" class="food" onclick={() => chooseVariant(variant)}>
              {@render mark(false)}
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
            hint={weightHint}
            saveLabel={m['nutrition.weight.save']()}
            removeLabel={ownWeight ? m['nutrition.weight.remove']() : undefined}
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

    <div
      class="results"
      class:open={searched !== null || loading.showing}
      aria-live="polite"
      aria-busy={loading.showing}
    >
      {#if loading.showing}
        <div class="skeletons" role="status" aria-label={m['nutrition.correct.loading']()}>
          {#each [0, 1, 2, 3] as row (row)}
            <Skeleton width={row % 2 ? '70%' : '85%'} height="1.25rem" />
          {/each}
        </div>
      {:else if failed}
        <p class="quiet">{m['nutrition.correct.failed']()}</p>
      {:else if searched === null}
        <!-- Nothing asked yet: the similar foods above are the offer. -->
      {:else if others.length === 0}
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

    {#if packaged.length > 0}
      <h3 class="sectionTitle">{m['nutrition.package.title']()}</h3>
      <ul class="foods" aria-label={m['nutrition.package.title']()}>
        {#each packaged as food (food.code)}
          <li>
            <button type="button" class="food" onclick={() => onchoose(hitOf(food))}>
              {@render mark(false)}
              <span class="words">
                <span class="name">{food.name}</span>
                <span class="quiet">
                  {per100(food.energyKcal)} · {m['nutrition.fromPackage']()}
                </span>
              </span>
            </button>
          </li>
        {/each}
      </ul>
    {/if}

    {#if householdId && name !== null}
      {#if adding}
        <section class="package" aria-labelledby="nutrition-package-title">
          <h3 id="nutrition-package-title" class="sectionTitle">
            {m['nutrition.package.add']()}
          </h3>
          <PackageFoodForm {name} onsave={addFood} oncancel={() => (adding = false)} />
        </section>
      {:else}
        <p class="addPackage">
          <span class="quiet">{m['nutrition.package.missing']()}</span>
          <Button size="sm" variant="ghost" onclick={() => (adding = true)}>
            {m['nutrition.package.add']()}
          </Button>
        </p>
      {/if}
    {/if}
  </div>

  {#snippet footer()}
    {#if corrected && !inheritedChoice}
      <Button onclick={onreset}>
        {inherits ? m['nutrition.correct.undo']() : m['nutrition.correct.reset']()}
      </Button>
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

  .package {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .addPackage {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-1) var(--space-2);
    margin: 0;
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

  /* Room for the answer once a search runs, so the sheet does not jump as results arrive. */
  .results.open {
    min-height: 12rem;
  }
</style>
