<script lang="ts">
  import { resolve } from '$app/paths';
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  /**
   * A recipe saved as only a title (the new-recipe page invites that): said for what it is, with the way to finish it,
   * rather than an empty recipe with nothing to cook.
   */
  interface Props {
    recipeId: string;
  }

  let { recipeId }: Props = $props();
</script>

<section class="stub" aria-label={m['recipe.stub.title']()}>
  <p class="title">{m['recipe.stub.title']()}</p>
  <p class="body">{m['recipe.stub.body']()}</p>
  <Button variant="primary" href={resolve('/(app)/recipes/[recipeId]/edit', { recipeId })}>
    {m['recipe.stub.finish']()}
  </Button>
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
