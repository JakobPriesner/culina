<script lang="ts">
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  interface Props {
    /** Whether the shop finished while somebody was here to see it. */
    celebrate: boolean;
  }

  let { celebrate }: Props = $props();
</script>

<!-- The shop is done. Said out loud, because a page whose only content is
     a struck-through list looks like a page that has lost something. -->
<div class="finished">
  {#if celebrate}<Olli pose="celebrating" size="sm" />{/if}
  <p>{m['shopping.allBought']()}</p>
</div>

<style>
  .finished {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    margin-bottom: var(--space-8);
  }
</style>
