<script lang="ts">
  import { VisuallyHidden } from '$ds';
  import { m } from '$shell/i18n';

  import { nbsp } from './format';

  /**
   * "≈" before a number that rests partly on typical weights, with the word for whoever cannot see the sign.
   * Nothing at all when the value is exact, so it can sit before every number.
   */
  let { estimated = false }: { estimated?: boolean } = $props();
</script>

{#if estimated}<span class="mark" aria-hidden="true">≈{nbsp}</span><VisuallyHidden
    >{m['nutrition.estimatedValue']()}{nbsp}</VisuallyHidden
  >{/if}

<style>
  .mark {
    color: var(--text-muted);
    font-weight: var(--weight-regular);
  }
</style>
