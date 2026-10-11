<script lang="ts">
  import { resolve } from '$app/paths';

  import { upcoming } from '$features/planning/upcoming.svelte';
  import { slotLabel } from '$features/planning/slots';
  import { dayOf } from '$features/planning/weekDates';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { m } from '$shell/i18n';
  import { preferences } from '$shell/preferences.svelte';

  /**
   * Where this recipe is in the loop, under its title: planned for a day, on the shopping list. Each is a
   * link to that place, so the answer to "did I already plan this?" is on the recipe, not a tab away.
   * Nothing at all when neither is true; the cooked count is already in the meta line.
   */
  interface Props {
    recipeId: string;
    householdId: string;
  }

  let { recipeId, householdId }: Props = $props();

  $effect(() => {
    void upcoming.load(householdId);
  });

  $effect(() => {
    void shopping.load(householdId);
  });

  const planned = $derived(upcoming.nextFor(recipeId));

  /** Still to buy for this recipe; a line already in the trolley is done with. `sources` can be missing from a list cached before it existed, as the list row also allows. */
  const listed = $derived(
    shopping.householdId === householdId &&
      shopping.items.some(
        (item) =>
          !item.isChecked && (item.sources ?? []).some((source) => source.recipeId === recipeId)
      )
  );

  const when = $derived(
    planned
      ? `${new Intl.DateTimeFormat(preferences.locale, { weekday: 'short', day: 'numeric', month: 'short' }).format(dayOf(planned.date))}, ${slotLabel[planned.slot]()}`
      : ''
  );

  const planHref = $derived(
    `${resolve('/(app)/plan')}${planned && planned.week > 0 ? `?week=${planned.week}` : ''}`
  );
</script>

{#if planned || listed}
  <ul class="whereabouts">
    {#if planned}
      <li>
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the week is appended. -->
        <a class="chip" href={planHref}>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <rect x="3.5" y="5.5" width="17" height="15" rx="2" />
            <path d="M8 3.5v4M16 3.5v4M3.5 10h17" />
          </svg>
          {m['recipe.whereabouts.planned']({ when })}
        </a>
      </li>
    {/if}

    {#if listed}
      <li>
        <a class="chip" href={resolve('/(app)/shopping')}>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M4 8h16l-1.4 10a2 2 0 0 1-2 1.7H7.4a2 2 0 0 1-2-1.7Z" />
            <path d="M9 8 12 3l3 5" />
          </svg>
          {m['plan.onList']()}
        </a>
      </li>
    {/if}
  </ul>
{/if}

<style>
  .whereabouts {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: var(--space-3) 0 0;
    list-style: none;
  }

  /* Quiet capsules: they report, so they are tinted rather than filled with the accent. */
  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control-sm);
    padding-inline: var(--space-3);
    border-radius: var(--radius-full);
    background: var(--surface-accent-subtle);
    color: var(--text);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    transition:
      background-color var(--duration-fast) var(--ease-out),
      transform var(--duration-fast) var(--ease-out);
  }

  .chip:hover {
    background: var(--surface-highlight);
  }

  .chip:active {
    transform: scale(var(--press-scale));
  }

  .chip svg {
    flex: none;
    width: var(--space-4);
    height: var(--space-4);
    color: var(--accent);
  }

  @media print {
    .whereabouts {
      display: none;
    }
  }
</style>
