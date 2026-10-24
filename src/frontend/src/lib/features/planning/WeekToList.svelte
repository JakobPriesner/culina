<script lang="ts">
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { mealPlan } from './mealPlan.svelte';
  import { weekTitle } from './weekDates';

  /**
   * Woche's seam (docs/woche-design.md §5.2): what the week on screen still needs, at the top of the list, with the one
   * request that puts it there. The lines arriving below say it is done, so no toast does; nothing shows once nothing
   * is missing. One sentence with its action at the end, so it pushes the list down as little as it can.
   */
  interface Props {
    householdId: string;
    /** The Monday of a week on screen, as the plan was read for it. */
    week: string;
    /** That week in weeks from this one, which names it. */
    offset: number;
  }

  let { householdId, week, offset }: Props = $props();

  let busy = $state(false);

  const missing = $derived(mealPlan.unshoppedIn(week).length);

  async function add() {
    busy = true;

    const failure = await shopping.addPlannedWeek(householdId, week);

    if (!failure) await mealPlan.refresh(householdId);

    busy = false;

    if (failure) toaster.show({ message: () => explain(failure), tone: 'danger' });
  }
</script>

{#if missing > 0}
  <p class="seam">
    {m['woche.seam.missing']({ week: weekTitle(offset), count: missing })}
    <button type="button" class="add" disabled={busy} aria-busy={busy} onclick={() => void add()}>
      {m['woche.seam.add']()}
    </button>
  </p>
{/if}

<style>
  .seam {
    margin-block: var(--space-3);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
    font-size: var(--text-sm);
  }

  /* The sentence's own verb: accent, firm, and a whole target even where it wraps. */
  .add {
    display: inline-flex;
    align-items: center;
    min-height: 1.5rem;
    margin-inline-start: var(--space-1);
    padding: 0;
    border: none;
    background: none;
    color: var(--accent);
    font: inherit;
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
    white-space: nowrap;
    cursor: pointer;
  }

  .add:disabled {
    cursor: progress;
    opacity: 0.6;
  }
</style>
