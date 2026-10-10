<script lang="ts">
  import { dayWords } from '$features/nutrition/dayFigure';
  import { recipeAnswers } from '$features/nutrition/stores/recipeAnswers.svelte';

  import type { PlannedMeal } from './mealPlan.svelte';

  /**
   * What one person has on this day, in one quiet line below its meals. It appears when every meal's
   * answer is in (a sum that grew as answers arrived would be a smaller number than the truth for a moment),
   * takes no room before that and says nothing for a day with nothing to add up. The meals' own page asks;
   * this only reads.
   */
  interface Props {
    meals: readonly PlannedMeal[];
    /** Whose corrections the figures follow: the plan's household. */
    householdId: string | null;
  }

  let { meals, householdId }: Props = $props();

  const words = $derived.by(() => {
    if (!householdId || meals.length === 0) {
      return null;
    }

    const figures = meals.map((meal) => ({
      title: meal.title,
      answer: recipeAnswers.of(meal.recipeId, householdId)
    }));

    return figures.every(({ answer }) => answer.settled)
      ? dayWords(
          figures.map(({ title, answer }) => ({
            title,
            nutrition: answer.settled ? answer.nutrition : null
          }))
        )
      : null;
  });
</script>

{#if words}
  <p class="nutrition" data-plan-nutrition>{words}</p>
{/if}

<style>
  .nutrition {
    min-width: 0;
    color: var(--text-muted);
    font-size: var(--text-xs);
    line-height: var(--leading-normal);
    overflow-wrap: anywhere;
  }
</style>
