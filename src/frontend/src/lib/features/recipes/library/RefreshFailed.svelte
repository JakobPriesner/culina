<script lang="ts">
  import { Button } from '$ds';
  import { m } from '$shell/i18n';

  /** A refresh failed while the last good content stays on screen; says so next to it instead of replacing it. */
  interface Props {
    onretry: () => void;
  }

  let { onretry }: Props = $props();
</script>

<div class="notice" role="status">
  <p>{m['recipes.refreshFailed']()}</p>
  <Button onclick={onretry}>{m['error.retry']()}</Button>
</div>

<style>
  .notice {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    margin-bottom: var(--space-4);
    color: var(--text-muted);
    font-size: var(--text-sm);
  }
</style>
