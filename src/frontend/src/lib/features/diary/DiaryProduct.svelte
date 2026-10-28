<script lang="ts">
  import { untrack } from 'svelte';

  import type { AppError } from '$api';
  import { Button, Field, TextInput } from '$ds';
  import PackageFoodForm from '$features/nutrition/PackageFoodForm.svelte';
  import {
    householdFoods,
    type HouseholdFood,
    type Label
  } from '$features/nutrition/stores/householdFoods.svelte';
  import {
    lookupCatalogue,
    productTitle,
    type CatalogueProduct
  } from '$features/scanning/catalogue';
  import { readGtin } from '$features/scanning/gtin';
  import { lookupProduct } from '$features/scanning/products';
  import Viewfinder from '$features/scanning/Viewfinder.svelte';
  import { explain } from '$shell/explain';
  import { haptics } from '$shell/haptics';
  import DiaryBack from './DiaryBack.svelte';
  import { formatNumber, m } from '$shell/i18n';

  import type { FoodChoice } from './DiaryAddSheet.svelte';

  /**
   * A package into the diary (docs/diary-design.md §6.4): scan it or type its digits, and a package the household
   * kept goes straight on to its amount. One it does not know yet is looked up at Open Food Facts when the server
   * does that, shown with its source and doubts, and taken over in one tap or checked against the label first;
   * otherwise its label is typed once. Either way it becomes the household's own food, so the next scan is one step.
   */
  interface Props {
    householdId: string;
    /** Whether the server looks barcodes up at Open Food Facts. */
    openFoodFacts: boolean;
    /** A barcode already chosen (from a search at Open Food Facts); null starts the camera. */
    gtin: string | null;
    onfood: (choice: FoodChoice) => void;
    onback: () => void;
  }

  let { householdId, openFoodFacts, gtin, onfood, onback }: Props = $props();

  type Phase =
    | { readonly kind: 'scanning' }
    | { readonly kind: 'looking' }
    | { readonly kind: 'offer'; readonly product: CatalogueProduct }
    | {
        readonly kind: 'label';
        readonly gtin: string;
        readonly name: string;
        /** What Open Food Facts gave, for the person to check; null when it is typed from nothing. */
        readonly offered: CatalogueProduct | null;
        /** Why there is nothing to start from, when Open Food Facts could not be asked. */
        readonly notice: string | null;
      }
    | { readonly kind: 'failed'; readonly gtin: string; readonly error: AppError };

  let phase = $state<Phase>({ kind: 'scanning' });
  let typed = $state('');
  let invalid = $state(false);
  let taking = $state(false);
  let failure = $state<string | null>(null);

  // A chosen barcode is looked up at once; the camera is for when there is none.
  $effect(() => {
    const chosen = gtin;

    if (chosen) untrack(() => void resolve(chosen));
  });

  function detected(code: string) {
    typed = code;
    void resolve(code);
  }

  const choiceOf = (food: HouseholdFood): FoodChoice => ({
    code: food.code,
    householdId,
    subject: { kind: 'food', name: food.name, kcalPer100Grams: food.energyKcal, units: [] }
  });

  async function resolve(code: string) {
    const found = readGtin(code);

    if (found === null) {
      phase = { kind: 'scanning' };
      invalid = true;

      return;
    }

    phase = { kind: 'looking' };
    failure = null;

    const known = await lookupProduct(householdId, found);

    if (!known.ok) {
      phase = { kind: 'failed', gtin: found, error: known.error };

      return;
    }

    // What the household kept wins, even over Open Food Facts: straight on to how much.
    if (known.value?.food) {
      haptics.step();
      onfood(choiceOf(known.value.food));

      return;
    }

    const named = known.value?.name ?? '';

    if (!openFoodFacts) {
      phase = { kind: 'label', gtin: found, name: named, offered: null, notice: null };

      return;
    }

    const offered = await lookupCatalogue(found);

    if (!offered.ok) {
      phase = {
        kind: 'label',
        gtin: found,
        name: named,
        offered: null,
        notice: explain(offered.error)
      };

      return;
    }

    if (offered.value === null) {
      phase = { kind: 'label', gtin: found, name: named, offered: null, notice: null };

      return;
    }

    haptics.step();
    phase = { kind: 'offer', product: offered.value };
  }

  function submitDigits(event: SubmitEvent) {
    event.preventDefault();

    if (readGtin(typed) === null) {
      invalid = true;

      return;
    }

    void resolve(typed);
  }

  /** Keeps the label as the household's food of this package, and goes on to the amount. */
  async function keep(
    label: Label,
    code: string,
    fromCatalogue: boolean
  ): Promise<AppError | null> {
    const kept = await householdFoods.add(householdId, label, { gtin: code, fromCatalogue });

    if (!kept.ok) return kept.error;

    onfood(choiceOf(kept.value));

    return null;
  }

  async function take(product: CatalogueProduct) {
    taking = true;
    const error = await keep(
      { name: productTitle(product).slice(0, 120), ...product.label },
      product.gtin,
      true
    );
    taking = false;
    failure = error ? explain(error) : null;
  }

  function check(product: CatalogueProduct) {
    phase = {
      kind: 'label',
      gtin: product.gtin,
      name: productTitle(product).slice(0, 120),
      offered: product,
      notice: null
    };
  }

  /** The label's values to start from; kilocalories nobody entered are left empty rather than shown as 0. */
  const initialOf = (product: CatalogueProduct | null) =>
    product === null
      ? undefined
      : {
          ...product.label,
          energyKcal: product.doubts.includes('noEnergy') ? null : product.label.energyKcal
        };

  const doubtText: Record<string, () => string> = {
    noEnergy: () => m['diary.scan.doubt.noEnergy'](),
    macrosMissing: () => m['diary.scan.doubt.macrosMissing'](),
    energyMismatch: () => m['diary.scan.doubt.energyMismatch'](),
    unitMismatch: () => m['diary.scan.doubt.unitMismatch'](),
    impossible: () => m['diary.scan.doubt.impossible']()
  };

  const rows = (product: CatalogueProduct) =>
    [
      [
        m['nutrition.package.energyKcal'](),
        product.doubts.includes('noEnergy') ? null : product.label.energyKcal
      ],
      [m['nutrition.package.fat'](), product.label.fat],
      [m['nutrition.package.carbohydrate'](), product.label.carbohydrate],
      [m['nutrition.package.sugars'](), product.label.sugars],
      [m['nutrition.package.protein'](), product.label.protein],
      [m['nutrition.package.salt'](), product.label.salt]
    ] as const;

  const valueText = (value: number | null) =>
    value === null ? '–' : formatNumber(value, { maximumFractionDigits: 1 });
</script>

<div class="product">
  <DiaryBack onclick={onback} />

  {#if phase.kind === 'scanning'}
    <h3>{m['diary.scan.title']()}</h3>
    <Viewfinder ondetect={detected} />

    <form class="digits" onsubmit={submitDigits} novalidate>
      <Field
        label={m['scan.digits.label']()}
        hint={m['scan.digits.hint']()}
        error={invalid ? m['scan.digits.invalid']() : undefined}
      >
        {#snippet children({ id, describedBy, invalid: bad })}
          <TextInput
            {id}
            {describedBy}
            invalid={bad}
            bind:value={typed}
            inputmode="numeric"
            autocomplete="off"
            oninput={() => (invalid = false)}
          />
        {/snippet}
      </Field>
      <Button type="submit">{m['scan.digits.look']()}</Button>
    </form>
  {:else if phase.kind === 'looking'}
    <p class="quiet" role="status">{m['scan.looking']()}</p>
  {:else if phase.kind === 'offer'}
    {@const product = phase.product}
    {@const usable = !product.doubts.includes('noEnergy')}
    <section class="offer" aria-labelledby="diary-product-name">
      <div>
        <h3 id="diary-product-name">{product.name}</h3>
        {#if product.brand || product.quantity}
          <p class="quiet">{[product.brand, product.quantity].filter(Boolean).join(' · ')}</p>
        {/if}
      </div>

      <table class="values">
        <caption class="quiet">{m['diary.scan.per100']()}</caption>
        <tbody>
          {#each rows(product) as [label, value] (label)}
            <tr>
              <th scope="row">{label}</th>
              <td>{valueText(value)}</td>
            </tr>
          {/each}
        </tbody>
      </table>

      {#each product.doubts as doubt (doubt)}
        {#if doubtText[doubt]}
          <p class="doubt">{doubtText[doubt]()}</p>
        {/if}
      {/each}

      <p class="source">
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- Open Food Facts' own page, built by the server from the barcode. -->
        <a href={product.url} target="_blank" rel="noopener noreferrer"
          >{m['diary.scan.source']()}</a
        >
        · {m['diary.scan.licence']()}
      </p>

      {#if failure}
        <p class="failure" role="alert">{failure}</p>
      {/if}

      <div class="actions">
        {#if usable}
          <Button variant="primary" loading={taking} onclick={() => void take(product)}>
            {m['diary.scan.take']()}
          </Button>
        {/if}
        <Button variant={usable ? 'secondary' : 'primary'} onclick={() => check(product)}>
          {m['diary.scan.check']()}
        </Button>
      </div>
    </section>
  {:else if phase.kind === 'label'}
    {@const current = phase}
    <h3>{m['diary.scan.label.title']()}</h3>
    <p class="quiet">
      {current.offered
        ? m['diary.scan.label.check']()
        : openFoodFacts && !current.notice
          ? m['diary.scan.label.unknownElsewhere']()
          : m['diary.scan.label.unknown']()}
    </p>
    {#if current.notice}
      <p class="doubt" role="status">{current.notice}</p>
    {/if}
    {#key current}
      <PackageFoodForm
        name={current.name}
        initial={initialOf(current.offered)}
        onsave={(label) => keep(label, current.gtin, current.offered !== null)}
        oncancel={onback}
      />
    {/key}
  {:else}
    {@const current = phase}
    <p class="failure" role="alert">{explain(current.error)}</p>
    <Button onclick={() => void resolve(current.gtin)}>{m['scan.retry']()}</Button>
  {/if}
</div>

<style>
  .product,
  .offer,
  .digits {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .product {
    gap: var(--space-4);
  }

  h3 {
    margin: 0;
    font-size: var(--text-lg);
    overflow-wrap: anywhere;
  }

  .values {
    border-collapse: collapse;
    inline-size: 100%;
    font-size: var(--text-sm);
  }

  .values caption {
    padding-block-end: var(--space-1);
    text-align: start;
  }

  .values th,
  .values td {
    padding: var(--space-1) 0;
    border-block-end: 1px solid var(--border);
    font-weight: var(--weight-regular);
  }

  .values th {
    text-align: start;
  }

  .values td {
    font-variant-numeric: tabular-nums;
    text-align: end;
  }

  .quiet,
  .source {
    margin: 0;
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  .source a {
    color: inherit;
  }

  .doubt {
    margin: 0;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
    --field-surface: var(--field-surface-inset);
    font-size: var(--text-sm);
  }

  .failure {
    margin: 0;
    color: var(--text-danger);
    font-size: var(--text-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
