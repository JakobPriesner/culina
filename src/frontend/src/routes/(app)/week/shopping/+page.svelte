<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { session } from '$features/auth/session.svelte';
  import { mealPlan } from '$features/planning/mealPlan.svelte';
  import { mondayOf } from '$features/planning/weekDates';
  import WeekToList from '$features/planning/WeekToList.svelte';
  import ShoppingListView from '$features/shopping/ShoppingListView.svelte';
  import { m } from '$shell/i18n';
  import { listBeside, wocheHref } from '$shell/woche.svelte';
  import WocheSwitch from '$shell/WocheSwitch.svelte';

  /** Woche's list: the household's shopping, with what this week still needs at its top. */
  const householdId = $derived(session.activeHouseholdId);
  const monday = mondayOf(0);

  $effect(() => {
    if (householdId) void mealPlan.load(householdId, monday);
  });

  /**
   * Where the list fits beside the week, the list alone gives way to the plan, on arrival or when the window widens.
   * The query goes along: `?mahlzeit=` marks its lines in the pane as here.
   */
  $effect(() => {
    if (listBeside.current) {
      // eslint-disable-next-line svelte/no-navigation-without-resolve -- The route is resolved; only the query is appended.
      void goto(`${wocheHref('plan')}${page.url.search}`, { replaceState: true });
    }
  });
</script>

<svelte:head><title>{m['shopping.title']()}</title></svelte:head>

<WocheSwitch view="list" />
<!-- Read in a column of its own measure, starting where the plan does, so flipping Plan | Liste moves nothing. -->
<div class="list">
  <ShoppingListView>
    {#snippet seam()}
      {#if householdId}<WeekToList {householdId} week={monday} offset={0} />{/if}
    {/snippet}
  </ShoppingListView>
</div>

<style>
  .list {
    max-width: var(--measure);
  }
</style>
