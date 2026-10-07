<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * Said instead of a form whose every keystroke the server would refuse: the
   * recipe belongs to another household, and this one only reads it.
   */
  interface Props {
    /** The household that owns it, when it is known. */
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
