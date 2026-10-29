<script lang="ts">
  import { session } from '$features/auth/session.svelte';
  import { mealPlan } from '$features/planning/mealPlan.svelte';
  import { mondayOf } from '$features/planning/weekDates';
  import WeekToList from '$features/planning/WeekToList.svelte';
  import ShoppingListView from '$features/shopping/ShoppingListView.svelte';
  import { m } from '$shell/i18n';
  import WocheSwitch from '$shell/WocheSwitch.svelte';

  /** Woche's list: the household's shopping, with what this week still needs at its top. */
  const householdId = $derived(session.activeHouseholdId);
  const monday = mondayOf(0);

  $effect(() => {
    if (householdId) void mealPlan.load(householdId, monday);
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
