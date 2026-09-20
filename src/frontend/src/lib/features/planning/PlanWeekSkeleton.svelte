<script lang="ts">
  import { Skeleton } from '$ds';
  import { m } from '$shell/i18n';

  // 7 days of the week, with typical varying numbers of planned meals
  const days = [
    { name: '4rem', num: '1.25rem', meals: 1 },
    { name: '4.5rem', num: '1.25rem', meals: 2 },
    { name: '5rem', num: '1.25rem', meals: 1 },
    { name: '4.5rem', num: '1.25rem', meals: 0 },
    { name: '3.5rem', num: '1.25rem', meals: 2 },
    { name: '4.5rem', num: '1.25rem', meals: 1 },
    { name: '4rem', num: '1.25rem', meals: 0 }
  ];
</script>

<ol class="week" aria-busy="true" aria-label={m['plan.title']()}>
  {#each days as day, i (i)}
    <li class="day" aria-hidden="true">
      <div class="name">
        <Skeleton width={day.name} height="1rem" />
        <Skeleton width={day.num} height="1rem" />
      </div>

      {#if day.meals > 0}
        <div class="meals">
          {#each Array.from({ length: day.meals }) as _, mIndex (mIndex)}
            <div class="meal-card">
              <Skeleton width="var(--space-8)" height="var(--space-8)" shape="block" />
              <div class="meal-words">
                <Skeleton width="80%" height="0.875rem" />
                <Skeleton width="50%" height="0.75rem" />
              </div>
            </div>
          {/each}
        </div>
      {/if}

      <div class="add-button">
        <Skeleton width="4rem" height="1.75rem" shape="block" />
      </div>
    </li>
  {/each}
</ol>

<style>
  .week {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  @media (min-width: 48rem) {
    .week {
      grid-template-columns: repeat(7, minmax(0, 1fr));
      gap: var(--space-3);
    }
  }

  .day {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
    min-height: 12rem;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-sunken);
  }

  .name {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
    min-height: 1.5rem;
  }

  .meals {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    flex: 1;
  }

  .meal-card {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--surface-raised);
    min-height: var(--control-sm);
  }

  .meal-words {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    flex: 1;
    min-width: 0;
  }

  .add-button {
    margin-top: auto;
  }
</style>
