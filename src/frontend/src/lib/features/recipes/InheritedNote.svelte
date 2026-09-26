<script lang="ts">
  import { Button } from '$ds';

  import { m } from '$shell/i18n';

  /**
   * Why a recipe has no Edit and no Delete.
   *
   * A recipe this household inherits can be cooked, planned and shopped for
   * here and changed only where it belongs. Taking the controls away without a
   * word would read as a bug; this says whose it is instead — and, for somebody
   * who is in that household too, offers the one step that makes it editable.
   */
  interface Props {
    /** The household it belongs to, when this person can know its name. */
    household: string | null;
    /** Given when they are in that household, to go and look at it from there. */
    onswitch?: () => void;
  }

  let { household, onswitch }: Props = $props();
</script>

<aside class="note">
  <p>
    {household ? m['recipe.inherited.note']({ household }) : m['recipe.inherited.unknown']()}
  </p>

  {#if onswitch && household}
    <Button variant="secondary" size="sm" onclick={onswitch}>
      {m['recipe.inherited.switch']({ household })}
    </Button>
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
</style>
