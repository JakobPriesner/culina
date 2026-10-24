<script lang="ts">
  import { resolve } from '$app/paths';
  import { tick } from 'svelte';

  import { ActionMenu, Button, Checkbox, IconButton, TextInput } from '$ds';

  import type { MealSlot } from '$features/planning/mealPlan.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { upcoming } from '$features/planning/upcoming.svelte';
  import { unitLabel } from '$features/recipes/quantityLabels';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { haptics } from '$shell/haptics';
  import { formatDate, m } from '$shell/i18n';
  import { amountOf } from './shoppingAmount';
  import { nameOf, sectionOrder, type Section } from './sections';
  import { planDayHref } from '$features/planning/weekDates';
  import { provenance } from '$features/planning/provenance.svelte';
  import { lineMarks } from './lineMarks.svelte';
  import { shopping, type ShoppingItem } from './stores/shopping.svelte';

  /**
   * One line, read while walking: the whole row is the tick, and the amount is rounded here, last (the server keeps the exact sum so added recipes do not compound error).
   * The section is a guess, corrected on the line itself and remembered for that name.
   */
  interface Props {
    item: ShoppingItem;
    oncheck: (isChecked: boolean) => void;
    onremove: () => void;
    onmove: (section: Section) => void;
    /** Whether this line says why it is listed apart; false for all but the last of several lines of one thing. */
    noted?: boolean;
    /** Sets how much to buy, in the line's own unit. */
    onquantity?: (amount: number) => void;
    /** Another line's name the household's names suggest this is the same thing as. */
    sameAs?: string | null;
    /** Accepts that suggestion: one name from now on, and one line where the amounts add up. */
    onmerge?: () => void;
  }

  let {
    item,
    oncheck,
    onremove,
    onmove,
    noted = true,
    onquantity,
    sameAs = null,
    onmerge
  }: Props = $props();

  let changing = $state(false);
  let typed = $state('');

  async function changeAmount() {
    typed = item.quantity === null || item.quantity === undefined ? '' : String(item.quantity);
    changing = true;
    await tick();
    document.getElementById(`shopping-quantity-${item.itemId}`)?.focus();
  }

  function saveAmount(event: SubmitEvent) {
    event.preventDefault();

    const amount = Number(String(typed).replace(',', '.'));

    if (Number.isFinite(amount) && amount > 0) {
      onquantity?.(amount);
      changing = false;
    }
  }

  let showingSources = $state(false);

  const amount = $derived(amountOf(item.quantity, item.unit));
  /** Just put here by a plan or a recipe: a brief tint says so where the list is on screen. */
  const arrived = $derived(shopping.arrived.includes(item.itemId));
  /** For the planned meal whose "Auf der Liste" led here. */
  const marked = $derived(
    lineMarks.entryId !== null &&
      (item.sources ?? []).some((source) => source.planEntryId === lineMarks.entryId)
  );
  const sources = $derived(item.sources ?? []);

  /** The meal pointed at in the week beside, if this line is for it: lit, and its share said where it is merged. */
  const pointedSource = $derived(
    provenance.meal === null
      ? undefined
      : sources.find((source) => source.planEntryId === provenance.meal)
  );
  const share = $derived(
    pointedSource && sources.length > 1 ? amountOf(pointedSource.quantity, pointedSource.unit) : ''
  );
  const plannedIn = $derived(
    sources.flatMap((source) => (source.planEntryId ? [source.planEntryId] : []))
  );

  function point(event: PointerEvent | FocusEvent, on: boolean) {
    if ('pointerType' in event && event.pointerType !== 'mouse') return;
    if (!on && event instanceof FocusEvent && event.currentTarget instanceof Element) {
      if (event.relatedTarget instanceof Node && event.currentTarget.contains(event.relatedTarget))
        return;
    }
    provenance.pointLine(on ? plannedIn : []);
  }
  /** What no recipe accounts for (typed by hand, or left from elsewhere), so the sources add up to the line. */
  const other = $derived(item.otherQuantity ? amountOf(item.otherQuantity, item.unit) : '');

  const plannedFor = (date: string, slot: string | null | undefined) =>
    m['shopping.source.planned']({
      date: formatDate(new Date(`${date}T12:00:00`), {
        weekday: 'short',
        day: 'numeric',
        month: 'short'
      }),
      // A slot this client does not know is left out rather than guessed at.
      slot: (slot && slotLabel[slot as MealSlot]?.()) || ''
    });

  /** The recipe at the servings its planned meal is for, while the plan still holds it. */
  const sourceHref = (source: (typeof sources)[number]) =>
    hrefAtYield(
      resolve('/(app)/recipes/[recipeId]', { recipeId: source.recipeId }),
      source.planEntryId ? upcoming.mealOf(source.planEntryId)?.servings : null
    );

  /** Moves the line, but only when the section chosen is a different one. */
  function move(section: Section) {
    if (section !== item.section) {
      onmove(section);
    }
  }
</script>

<li
  class="row"
  class:bought={item.isChecked}
  class:arrived
  class:marked
  class:related={pointedSource !== undefined}
  onpointerenter={(event) => point(event, true)}
  onpointerleave={(event) => point(event, false)}
  onfocusin={(event) => point(event, true)}
  onfocusout={(event) => point(event, false)}
>
  <div class="check">
    <Checkbox
      checked={item.isChecked}
      label={item.name}
      describedBy={amount ? `shopping-amount-${item.itemId}` : undefined}
      onchange={(isChecked) => {
        haptics.tick();
        oncheck(isChecked);
      }}
    />
  </div>
  {#if amount}<span class="amount" id="shopping-amount-{item.itemId}"
      >{share ? m['shopping.share']({ part: share, whole: amount }) : amount}</span
    >{/if}
  <div class="actions">
    <ActionMenu wrap>
      {#snippet trigger({ popovertarget })}
        <IconButton
          label={m['shopping.itemActions']({ name: item.name })}
          size="sm"
          {popovertarget}
        >
          <svg viewBox="0 0 24 24" fill="currentColor">
            <circle cx="5" cy="12" r="1.6" /><circle cx="12" cy="12" r="1.6" /><circle
              cx="19"
              cy="12"
              r="1.6"
            />
          </svg>
        </IconButton>
      {/snippet}
      {#if sources.length > 0}
        <button
          class="item"
          type="button"
          aria-expanded={showingSources}
          onclick={() => (showingSources = !showingSources)}
        >
          {showingSources
            ? m['shopping.sources.hide']()
            : m['shopping.sources.show']({ count: sources.length })}
        </button>
      {/if}
      {#if onquantity && !item.isChecked}
        <button type="button" class="item" onclick={() => void changeAmount()}>
          {m['shopping.quantity.change']()}
        </button>
      {/if}
      <button type="button" class="item danger" onclick={onremove}
        >{m['shopping.remove']({ name: item.name })}</button
      >
      {#if !item.isChecked}
        <hr class="separator" />
        <p class="heading">{m['shopping.move.heading']()}</p>
        <div class="sections">
          {#each sectionOrder as section (section)}
            {@const current = section === item.section}
            <button
              type="button"
              class="item"
              aria-current={current || undefined}
              onclick={() => move(section)}
            >
              <span class="mark" aria-hidden="true">
                {#if current}
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"><path d="m5 12 5 5 9-10" /></svg
                  >
                {/if}
              </span>
              {nameOf(section)}
            </button>
          {/each}
        </div>
      {/if}
    </ActionMenu>
  </div>
  {#if changing}
    <form class="quantity" onsubmit={saveAmount}>
      <TextInput
        id="shopping-quantity-{item.itemId}"
        label={m['shopping.quantity.label']({ name: item.name })}
        type="number"
        inputmode="decimal"
        min={0}
        step={0.1}
        bind:value={typed}
      />
      {#if item.unit}<span class="unit">{unitLabel(item.unit)}</span>{/if}
      <Button size="sm" variant="primary" type="submit">{m['shopping.quantity.save']()}</Button>
      <Button size="sm" variant="ghost" onclick={() => (changing = false)}>
        {m['shopping.quantity.cancel']()}
      </Button>
    </form>
  {/if}
  {#if sameAs && onmerge && !item.isChecked}
    <p class="merge">
      {m['shopping.merge.ask']({ other: sameAs })}
      <button type="button" class="merge-action" onclick={onmerge}>
        {m['shopping.merge.action']()}
      </button>
    </p>
  {/if}
  {#if item.isListedSeparately && !item.isChecked && noted}
    <p class="separate">{m['shopping.separate']()}</p>
  {/if}
  {#if showingSources}
    <ul class="sources">
      {#each sources as source, index (`${source.recipeId}-${source.plannedDate ?? 'direct'}-${index}`)}
        <li class="source">
          <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
          <a href={sourceHref(source)}>{source.recipeTitle}</a>
          {#if amountOf(source.quantity, source.unit)}<span class="source-amount"
              >{amountOf(source.quantity, source.unit)}</span
            >{/if}
          <!-- The way back from the list: the plan at that day. -->
          <!-- eslint-disable svelte/no-navigation-without-resolve -- planDayHref resolves the route; only the day is appended. -->
          {#if source.plannedDate}<a class="source-plan" href={planDayHref(source.plannedDate)}
              >{plannedFor(source.plannedDate, source.plannedSlot)}</a
            >{/if}
          <!-- eslint-enable svelte/no-navigation-without-resolve -->
        </li>
      {/each}
      {#if other}
        <li class="source">
          <span class="source-name">{m['shopping.source.other']()}</span>
          <span class="source-amount">{other}</span>
        </li>
      {/if}
    </ul>
  {/if}
</li>

<style>
  .row {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto auto;
    align-items: center;
    column-gap: var(--space-2);
    min-height: var(--control-lg);
    border-bottom: 1px solid var(--border);
    transition: background-color var(--duration-fast) var(--ease-out);
  }
  /* Asked for from the week: the meal's lines, ringed where they stand. */
  /* For the meal pointed at in the week beside. */
  .related {
    background: var(--surface-accent-subtle);
  }
  .marked {
    box-shadow: inset 3px 0 0 var(--accent);
    background: var(--surface-accent-subtle);
  }
  .arrived {
    animation: arrive 2.4s var(--ease-out);
  }
  @keyframes arrive {
    from,
    40% {
      background: var(--surface-accent-subtle);
    }
  }
  /* Still said, without the fade. */
  @media (prefers-reduced-motion: reduce) {
    .arrived {
      animation: none;
      background: var(--surface-accent-subtle);
    }
  }
  @media (hover: hover) {
    .row:hover {
      background: var(--surface-hover);
    }
  }
  .check {
    min-width: 0;
    padding-block: var(--space-1);
    overflow-wrap: anywhere;
  }
  .check :global(label) {
    width: 100%;
  }
  .bought .check,
  .bought .amount {
    color: var(--text-subtle);
    text-decoration: line-through;
  }
  .amount {
    grid-column: 2;
    color: var(--text-muted);
    font-size: var(--text-xs);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  .actions {
    grid-column: 3;
  }
  /* Too narrow to share a line: the amount drops under the name, so the name keeps its whole words. */
  @media (width < 24rem) {
    .row {
      grid-template-columns: minmax(0, 1fr) auto;
    }
    .amount {
      grid-column: 1;
      grid-row: 2;
      padding: 0 0 var(--space-2) calc(var(--space-6) + var(--space-3));
    }
    .actions {
      grid-column: 2;
      grid-row: 1;
    }
  }
  .sources {
    display: flex;
    grid-column: 1 / -1;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: var(--space-2) 0 var(--space-4) calc(var(--space-6) + var(--space-3));
    list-style: none;
  }
  .source {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .source a,
  .source-name {
    color: var(--text);
    font-weight: var(--weight-medium);
  }
  /* Eleven sections as a compact grid rather than a long column of buttons. */
  .sections {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .quantity {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    grid-column: 1 / -1;
    padding: 0 0 var(--space-3) calc(var(--space-6) + var(--space-3));
  }
  .quantity :global(input) {
    width: 7rem;
  }
  .unit {
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
  .merge {
    grid-column: 1 / -1;
    margin: 0;
    padding: 0 0 var(--space-2) calc(var(--space-6) + var(--space-3));
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  .merge-action {
    margin-inline-start: var(--space-2);
    padding: 0;
    border: 0;
    background: none;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-medium);
    cursor: pointer;
  }
  .separate {
    grid-column: 1 / -1;
    margin: 0;
    padding: 0 0 var(--space-2) calc(var(--space-6) + var(--space-3));
    color: var(--text-muted);
    font-size: var(--text-xs);
  }
  .source-amount {
    font-variant-numeric: tabular-nums;
  }
  .source-plan {
    color: var(--text-muted);
    text-decoration: underline;
    text-decoration-color: var(--border-strong);
    text-underline-offset: 0.2em;
  }
  .source-plan:hover {
    color: var(--text);
  }
</style>
