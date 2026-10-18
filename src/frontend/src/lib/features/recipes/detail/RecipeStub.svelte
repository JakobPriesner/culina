<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button } from '$ds';

  import { explain } from '$shell/explain';
  import { m } from '$shell/i18n';
  import { toaster } from '$shell/toaster.svelte';

  import { recipes } from '../stores/recipes.svelte';
  import type { Recipe } from '../types';
  import StubFill from './StubFill.svelte';

  /**
   * A recipe saved as only a title (the new-recipe page and the week's picker invite that): said for what it is,
   * with the ways to finish it, rather than an empty recipe with nothing to cook. Filling it keeps it the same recipe.
   */
  interface Props {
    recipe: Recipe;
  }

  let { recipe }: Props = $props();

  async function fill(patch: Partial<Recipe>) {
    const failure = await recipes.update({ ...recipe, ...patch });

    toaster.show(
      failure
        ? { message: () => explain(failure), tone: 'danger' }
        : { message: () => m['recipe.stub.filled'](), tone: 'success' }
    );
  }
</script>

<section class="stub" aria-label={m['recipe.stub.title']()}>
  <p class="title">{m['recipe.stub.title']()}</p>
  <p class="body">{m['recipe.stub.body']()}</p>
  <Button
    variant="primary"
    href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId: recipe.id })}
  >
    {m['recipe.stub.finish']()}
  </Button>
  <StubFill {recipe} onfill={fill} />
</section>

<style>
  .stub {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
    margin-bottom: var(--space-6);
    padding: var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--surface-sunken);
  }

  .title {
    margin: 0;
    font-weight: var(--weight-semibold);
  }

  .body {
    margin: 0 0 var(--space-2);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }

  @media print {
    .stub {
      display: none;
    }
  }
</style>
