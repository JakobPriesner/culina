<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  /** Why a recipe has no Edit or Delete: an inherited recipe is cooked, planned and shopped for here but changed where it belongs. Says whose it is and, for members of that household, offers the step that makes it editable. */
  interface Props {
    /** The household it belongs to, when this person can know its name. */
    household: string | null;
    /** Given when they are in that household, to go and look at it from there. */
    onswitch?: () => void;
    /** Makes this household its own copy, which it can change. */
    oncopy?: () => void;
  }

  let { household, onswitch, oncopy }: Props = $props();
</script>

<aside class="note">
  <p>
    {household ? m['recipe.inherited.note']({ household }) : m['recipe.inherited.unknown']()}
  </p>

  {#if oncopy || (onswitch && household)}
    <div class="actions">
      {#if oncopy}
        <Button variant="secondary" size="sm" onclick={oncopy}>{m['recipe.copy.action']()}</Button>
      {/if}
      {#if onswitch && household}
        <Button variant="ghost" size="sm" onclick={onswitch}>
          {m['recipe.inherited.switch']({ household })}
        </Button>
      {/if}
    </div>
  {/if}
</aside>

<style>
  .note {
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

  p {
    max-width: var(--measure);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
