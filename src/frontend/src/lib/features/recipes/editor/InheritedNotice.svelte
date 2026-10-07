<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /** Shown instead of a form the server would refuse every save of. */
  interface Props {
    owner: string | null | undefined;
    backHref: string;
  }

  let { owner, backHref }: Props = $props();
</script>

<div class="inherited">
  <h1 class="inherited-title">{m['editor.inherited.title']()}</h1>
  <p class="inherited-body">
    {owner ? m['recipe.inherited.note']({ household: owner }) : m['recipe.inherited.unknown']()}
  </p>
  <Button variant="primary" href={backHref}>{m['editor.inherited.back']()}</Button>
</div>

<style>
  .inherited {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-4);
    max-width: var(--measure);
    padding-block: var(--space-8);
  }

  .inherited-title {
    font-size: var(--text-2xl);
  }

  .inherited-body {
    color: var(--text-muted);
  }
</style>
