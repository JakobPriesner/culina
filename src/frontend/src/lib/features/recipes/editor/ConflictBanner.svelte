<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /**
   * A banner in the form rather than two buttons in the corner: this is the
   * only thing on the screen that needs a decision, and a decision offered in
   * the chrome is one nobody sees.
   */
  interface Props {
    onkeepmine: () => void;
    ontaketheirs: () => void;
  }

  let { onkeepmine, ontaketheirs }: Props = $props();
</script>

<div class="conflict" role="alert">
  <p class="conflict-title">{m['editor.conflict.title']()}</p>
  <p class="conflict-body">{m['editor.changedElsewhere']()}</p>

  <div class="conflict-actions">
    <Button variant="primary" onclick={onkeepmine}>
      {m['editor.conflict.keepMine']()}
    </Button>
    <Button onclick={ontaketheirs}>{m['editor.conflict.takeTheirs']()}</Button>
  </div>
</div>

<style>
  .conflict {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-4);
    border: 1px solid var(--warning);
    border-radius: var(--radius-lg);
    background: var(--warning-subtle);
    color: var(--text);
  }

  .conflict-title {
    font-weight: var(--weight-semibold);
  }

  .conflict-body {
    max-width: var(--measure);
    font-size: var(--text-sm);
    line-height: var(--leading-normal);
  }

  .conflict-actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin-top: var(--space-2);
  }
</style>
