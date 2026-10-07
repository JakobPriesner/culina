<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  import type { PresumableDiet } from './types';

  /**
   * "Ist das vegetarisch?", asked once of a recipe a search only presumed to be: a diet is certain in one direction only (no nutrition table),
   * so one tap turns a guess into a tag. Quiet on purpose, beside the recipe rather than in front.
   */
  interface Props {
    diet: PresumableDiet;
    busy?: boolean;
    onanswer: (keeps: boolean) => void;
  }

  let { diet, busy = false, onanswer }: Props = $props();
</script>

<aside class="question">
  <div class="text">
    <p class="ask">
      {diet === 'vegan'
        ? m['recipe.diet.question.vegan']()
        : m['recipe.diet.question.vegetarian']()}
    </p>
    <p class="why">{m['recipe.diet.why']()}</p>
  </div>

  <div class="actions">
    <Button variant="secondary" size="sm" disabled={busy} onclick={() => onanswer(true)}>
      {m['recipe.diet.yes']()}
    </Button>
    <Button variant="ghost" size="sm" disabled={busy} onclick={() => onanswer(false)}>
      {m['recipe.diet.no']()}
    </Button>
  </div>
</aside>

<style>
  .question {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
    margin-bottom: var(--space-6);
    padding: var(--space-3) var(--space-4);
    border-inline-start: 3px solid var(--accent);
    border-radius: var(--radius-md);
    background: var(--surface-accent-subtle);
    font-size: var(--text-sm);
  }

  .text {
    max-width: var(--measure);
  }

  .ask {
    font-weight: var(--weight-semibold);
  }

  .why {
    color: var(--text-muted);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
