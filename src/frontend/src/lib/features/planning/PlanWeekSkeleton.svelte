<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  /** The days' own layout while they load: the agenda's days, or (on a wide screen) its rows; as many as will show. */
  let { rows = false, count = 7 }: { rows?: boolean; count?: number } = $props();

  // Typical days, with varying numbers of planned meals.
  const typical = [
    { name: '4rem', meals: 1 },
    { name: '4.5rem', meals: 2 },
    { name: '5rem', meals: 1 },
    { name: '4.5rem', meals: 0 },
    { name: '3.5rem', meals: 2 },
    { name: '4.5rem', meals: 1 },
    { name: '4rem', meals: 0 }
  ];

  const days = $derived(typical.slice(0, count));
</script>

<ol class="week" class:rows aria-busy="true" aria-label={m['plan.title']()}>
  {#each days as day, i (i)}
    <li class="day" class:row={rows} aria-hidden="true">
      <div class="name">
        <Skeleton width={day.name} height="1rem" />
        <Skeleton width="3.5rem" height="0.875rem" />
      </div>

      {#if day.meals > 0}
        <div class="meals">
          {#each Array.from({ length: day.meals }) as _, mIndex (mIndex)}
            <div class="meal-card">
              <Skeleton width="2.5rem" height="2.5rem" shape="block" />
              <div class="meal-words">
                <Skeleton width="80%" height="0.875rem" />
                <Skeleton width="50%" height="0.75rem" />
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </li>
  {/each}
</ol>

<style>
  /* The agenda of `PlanWeek` and `PlanDay`, so nothing moves when the days arrive. */
  .week {
    display: flex;
    flex-direction: column;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .day {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
    padding: var(--space-3);
    border-top: 1px solid var(--border);
  }

  .day:first-child {
    border-top-color: transparent;
  }

  .name {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-height: 1.75rem;
  }

  .meals {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .meal-card {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: 3.5rem;
    padding: var(--space-2);
    border: 1px solid var(--border);
    border-radius: var(--radius-md);
    background: var(--surface-raised);
  }

  .meal-words {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    flex: 1;
    min-width: 0;
  }

  /* As `PlanDay`'s rows: the day in a column, its meals side by side. */
  .row {
    display: grid;
    grid-template-columns: 8rem minmax(0, 1fr) auto;
    align-items: start;
    column-gap: var(--space-4);
  }

  .row .name {
    flex-direction: column;
    align-items: flex-start;
    padding-top: var(--space-3);
  }

  .row .meals {
    flex-flow: row wrap;
  }

  .row .meal-card {
    flex: 1 1 15rem;
    max-width: 24rem;
  }
</style>
