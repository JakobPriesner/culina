<script lang="ts">
  import ShoppingListView from '$features/shopping/ShoppingListView.svelte';
  import WithdrawNote from '$features/shopping/WithdrawNote.svelte';
  import { m } from '$shell/i18n';

  import { weekOffsetOf } from './weekDates';
  import WeekToList from './WeekToList.svelte';

  /**
   * The list beside the week, live, with what each week on screen still needs at its top, where its lines arrive.
   * A meal dropped here puts its shopping on the list; the meal stays on its day.
   */
  interface Props {
    householdId: string | null;
    /** The first and last day the week shows, whose meals the list then says itself. */
    from: string;
    to: string;
    /** The Mondays of the weeks on screen. */
    weeks: readonly string[];
    /** A meal is carried over the list. */
    over: boolean;
  }

  let { householdId, from, to, weeks, over }: Props = $props();
</script>

<aside class="list" class:over aria-label={m['shopping.title']()} data-plan-list>
  <ShoppingListView pane shown={{ from, to }}>
    {#snippet seam()}
      {#if householdId}
        <WithdrawNote {householdId} />
        {#each weeks as monday (monday)}
          <WeekToList {householdId} week={monday} offset={weekOffsetOf(monday)} />
        {/each}
      {/if}
    {/snippet}
  </ShoppingListView>
</aside>

<style>
  /* Parted from the week by one straight hairline the height of the pane, and scrolled on its own. */
  .list {
    overflow-y: auto;
    overscroll-behavior: contain;
    padding-inline-start: var(--space-8);
    border-inline-start: 1px solid var(--border);
  }

  /* Scrolled on its own, so a sticky heading goes to the pane's top, not under the shell's header. */
  .list > :global(*) {
    --header-inset: 0px;
  }

  /* A meal carried over the list: dropping it here puts its shopping on, as a day's outline says it would land there. */
  .list.over {
    outline: 2px dashed var(--border-focus);
    outline-offset: calc(-1 * var(--space-1));
  }
</style>
